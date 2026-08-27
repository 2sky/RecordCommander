# Generation

The inverse direction: turning a live record back into a command string, and describing a
registration as a usage example or an LLM prompt.

**Status:** current as of `f03195e` · **Governing issues:** no single PRD (foundational domain).
**Priming skill:** [`.claude/skills/generation/SKILL.md`](../.claude/skills/generation/SKILL.md)

## What it is

Four public entry points, all in
[`RecordCommandRegistry.Generation.cs`](../RecordCommander/RecordCommandRegistry.Generation.cs):

| Entry point | Produces |
|---|---|
| `GenerateCommand(record, options?)` | a runnable `add …` line for one instance |
| `GetUsageExample(type, preferAliases?, filterProperty?)` | one-line `add x <Key> <Pos> [--Named=<Named>]` shape |
| `GetDetailedUsageExample(…)` | the same, plus `# Parameter descriptions:` comment block |
| `GetCustomCommandPrompt(name, describeParameters?)` | `cmd <a> [<b>]` shape for a registered custom command |

This domain reads the same `RecordRegistration` metadata that [`commands`](commands.md) builds, but
it is **deliberately not the mirror image** of it — see the asymmetry section. It is **not**
string→CLR coercion; that is [`conversion`](conversion.md), and this domain has its own separate
`ConvertValueToString`.

## Deliberate asymmetry — generate→parse is not a round trip

The two directions are independent implementations, and generation is the lossy one. Known breaks,
all owned by `Helpers.ConvertValueToString`:

- **Embedded quotes are not escaped** (open TODO in code). A value containing a `"` is emitted
  verbatim, so re-parsing it re-tokenizes at the wrong boundary.
- **Array elements are quoted individually when they contain a space,** producing `[a,"b c"]`. On
  the way back in, `conversion`'s bare-word branch is skipped (a `"` is present) *and* the
  single→double branch is skipped (no `'` present), so the value reaches `System.Text.Json`
  unchanged and throws. Generating a record with a spaced array element yields a command that
  cannot be run.
- **Only arrays are handled among collection types** (open TODO). An `IList`/`IEnumerable` property
  falls through to `ToString()` and emits its type name.

Round-tripping is asserted only for the narrow case in `Generation_Books_Roundtrip` — string and
`int` properties, no arrays, no embedded quotes. Treat anything wider as unsupported until a test
says otherwise.

## Invariants & rules

- **The unique key is always emitted, and must not be null** — `GenerateCommand` throws
  `InvalidOperationException` rather than producing a keyless command.
- **Positional emission stops at the first missing positional.** Once a positional property is
  absent from the candidate set, the loop `break`s and *all* remaining properties — including later
  positionals — are emitted as `--named` arguments. This is what keeps a partially-populated record
  generating a still-correct command. Owned by `RecordCommandRegistry.Generation.cs`.
- **`null` properties are always skipped,** regardless of `IgnoreDefaultValues`.
- **`[DefaultValue]` wins over `default(T)`.** `Helpers.IsDefaultValue` checks the attribute first;
  only without one does it compare against `Activator.CreateInstance` of the (nullable-unwrapped)
  type. A non-null reference type with no attribute is never considered default.
- **A registered record-typed value is replaced by its unique key** before emission, so references
  survive as keys rather than as `ToString()` output.
- **Registration lookup falls back to assignability.** `TryGetRegistration` tries an exact
  `RecordType` match, then the first registration whose `RecordType.IsAssignableFrom` the instance
  type — so a subclass generates under its registered base's name.
- **The unique key placeholder never uses an alias.** `GetUsageExample` emits
  `<{UniqueKeyProperty.Name}>` with no alias branch, even when `preferAliases` is true — while every
  other property in the same line does honour the alias.
- **`filterProperty` cannot exclude the unique key.** It is applied to positional and non-positional
  properties only; the key is always emitted and always described.

## Key files

| File | Role |
|---|---|
| [`RecordCommander/RecordCommandRegistry.Generation.cs`](../RecordCommander/RecordCommandRegistry.Generation.cs) | All four entry points, plus its own `file static class Helpers` (`IsDefaultValue`, `ConvertValueToString`, `GetAlias`, `GetTypeDescription`) |
| [`RecordCommander/CommandGenerationOptions.cs`](../RecordCommander/CommandGenerationOptions.cs) | `PreferAliases`, `UsePositionalProperties`, `IgnoreDefaultValues`, and the static mutable `Default` |

Note the partial-class split: this file declares a `file static class Helpers` that is **disjoint
from** the same-named class in `RecordCommandRegistry.cs`. Members are not shared between them;
adding a helper means choosing a file.

## Gotchas

- **`CommandGenerationOptions.Default` has a public setter.** It is process-global mutable state;
  one caller reassigning it changes generation for every other caller that passes `null` options.
- **An aliased property enters the candidate set twice and is deduped by tuple equality, not by
  property identity.** `AllProperties` holds one entry per name *and* per alias, all pointing at the
  same `PropertyInfo`; `GenerateCommand` iterates it into a `HashSet<(PropertyInfo, string, object?)>`
  where the duplicate collapses only because the tuple compares equal. A property whose getter
  returns a fresh, reference-equal-only object on each call would therefore emit twice.
- **Named-argument order follows `HashSet` enumeration, not registration order,** and removals occur
  during the positional pass. The tests assert exact output strings, so they are the de-facto pin on
  that ordering.
- **`GetTypeDescription` does not expand `[Flags]` enums** (open TODO) — a flags enum is described
  as a plain `enum (A|B|C)` list, giving an LLM no hint that values combine.
- **An all-filtered non-positional set still emits empty brackets.** The `[` … `]` is opened when
  `NonPositionalProperties.Length > 0`, before `filterProperty` runs, so filtering every one of them
  yields a trailing `[]`.
- **`GetTypeDescription` covers only `int`/`long`/`decimal`/`float`/`double` among numerics** (open
  TODO for `sbyte`, `byte`, `short`, `ushort`, `uint`, `ulong`, `char`); the rest fall through to a
  lowercased type name.

## Executable references

[`RecordCommander.Tests/RecordCommanderTests.cs`](../RecordCommander.Tests/RecordCommanderTests.cs).
Where prose here and a test disagree, **the test wins.**

- Option matrix (the authority on option interaction): `CommandGenerationOptions_CombinationsOfOptions`,
  `Generation_UsingDefaultOptions`, `Generation_UsingPositionalProperties`,
  `Generation_UsingNamedArguments`, `Generation_UsingAliases`.
- `[DefaultValue]` precedence: `GenerateCommand_IgnoresDefaultValues_WhenOptionIsTrue`,
  `GenerateCommand_IncludesDefaultValues_WhenOptionIsFalse`.
- Quoting of spaced values, and the round-trip that *is* guaranteed: `Generation_UsingSpaces`,
  `Generation_Books_Roundtrip`.
- Record-typed value → unique key: `Generation_UsingRelatedRecord`.
- Usage examples, aliasing, and `filterProperty`: `Generation_GetUsageExample`,
  `Generation_GetUsageExample_WithAlias`, `Generation_GetDetailedUsageExample`,
  `Generation_GetDetailedUsageExample_SkipSpokenLanguagesAndFlags`.
- Custom-command prompts and their optional-parameter brackets:
  `Generation_GetCustomCommandPrompt`, `…_WithOptional`, `…_DescribeParameters`.
- The AI use case end to end: `Parse_Generated_AI_Data`.

**Unasserted, and the riskiest thing to change:** the round-trip breaks listed above are all
code-derived, not test-pinned — no test generates a value containing a quote, an array with a spaced
element, or a non-array collection property. Nor is the empty-`[]` filter edge covered. Adding
escaping to `ConvertValueToString` is therefore a change no test will catch either way.

## Links

- Glossary: [`UBIQUITOUS_LANGUAGE.md`](../UBIQUITOUS_LANGUAGE.md) §
  [Generation](../UBIQUITOUS_LANGUAGE.md#generation) ·
  [Flagged ambiguities](../UBIQUITOUS_LANGUAGE.md#flagged-ambiguities) (notably *default value* and
  *Helpers*)
- Related domains: [`commands`](commands.md) (boundary: consumes the registration metadata built
  there, emits the grammar defined there) · [`conversion`](conversion.md) (boundary: the reverse
  direction, separately implemented, no round-trip guarantee)
- Priming skill: [`.claude/skills/generation/SKILL.md`](../.claude/skills/generation/SKILL.md)
