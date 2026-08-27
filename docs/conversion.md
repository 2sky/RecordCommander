# Conversion

String→CLR coercion for every value the library reads: positional args, named args, method-call
arguments, and custom-command parameters.

**Status:** current as of `f03195e` · **Governing issues:** no single PRD (foundational domain).
**Priming skill:** [`.claude/skills/conversion/SKILL.md`](../.claude/skills/conversion/SKILL.md)

## What it is

A single private method — `ConvertToType(TContext, string, Type)` in
[`RecordCommandRegistry.cs`](../RecordCommander/RecordCommandRegistry.cs) — plus the
`_customConverters` / `_customConverterTypeDescriptions` registries that let a caller override it.
Every value that enters the library passes through here exactly once.

It is **not** the tokenizer: by the time `ConvertToType` sees a value, [`commands`](commands.md) has
already decided where the token boundaries are and which member receives it. It is **not** the
reverse direction — `Helpers.ConvertValueToString` in [`generation`](generation.md) is a separate,
deliberately non-symmetric implementation.

## The fallback ladder (load-bearing)

Order is the specification, not an implementation detail. Each rung wins over everything below it:

1. **Nullable unwrap** — empty string → `null`, then continue against the underlying type
2. **`string`** — returned verbatim, no trimming, no unquoting
3. **Array** — bracket-normalize, then `System.Text.Json`
4. **Enum** — `Enum.TryParse`, case-insensitive
5. **Custom converters** — `_customConverters[targetType]`
6. **`DateTime` / `DateOnly`** (exact `yyyy-MM-dd`), **`TimeSpan`**, **`Guid`**
7. **Registered record type** — resolved via that registration's `FindRecord`
8. **`Convert.ChangeType`** with `CultureInfo.InvariantCulture`

Every rung except 1, 2 and 7 throws `ArgumentException` on failure rather than falling through.

## Invariants & rules

- **Custom converters are consulted *before* the built-in date/time/GUID handling.** That ordering
  is the whole mechanism by which a caller overrides `DateTime` or `Guid` parsing. Moving rung 5
  below rung 6 would silently disable those overrides. Owned by `RecordCommandRegistry.cs`.
- **Custom converters are keyed by exact `Type`,** not by assignability — a converter for a base
  type does not serve its subclasses.
- **Array values must be bracketed.** Unbracketed input throws
  "is not a valid array representation"; it is never treated as a single-element array.
- **Only `DateTime`/`DateOnly` are format-locked** to exact `yyyy-MM-dd`. `TimeSpan` uses
  `TimeSpan.TryParse` and `Guid` uses `Guid.TryParse`, both of which accept several shapes.
- **All primitive conversion is `InvariantCulture`,** so decimal separators do not follow the
  ambient culture. This is deliberate: commands are meant to be portable text.
- **A registered-record-typed property resolves by unique key and yields `null` when absent** — no
  create, no error. See the gotcha below.
- **`DateOnly` exists only on `net8.0`+.** Its rung sits inside `#if NET8_0_OR_GREATER`; on
  `netstandard2.0` a `DateOnly` property would fall through to `Convert.ChangeType` and throw.

### Array normalization

Three input forms are rewritten to JSON before `System.Text.Json` deserializes into
`List<TElement>` and then `.ToArray()`:

| Input | Rule applied |
|---|---|
| `["a","b"]` | already JSON — passed through |
| `['a','b']` | single→double quote replacement, **only when no `"` is present anywhere** |
| `[a,b]` | applied only when the value contains **neither** `'` nor `"`; splits on `,`, trims, quotes each, drops empties |

`[]` normalizes to an empty array.

## Key files

| File | Role |
|---|---|
| [`RecordCommander/RecordCommandRegistry.cs`](../RecordCommander/RecordCommandRegistry.cs) | `ConvertToType`, `RegisterCustomConverter`, `HasCustomConverter`, `CustomConverters` |
| [`RecordCommander/RecordCommandRegistry.Generation.cs`](../RecordCommander/RecordCommandRegistry.Generation.cs) | `Helpers.GetTypeDescription` — how a type is *described* to a human or an LLM; consumes `_customConverterTypeDescriptions` |

## Gotchas

- **A missing related record is indistinguishable from an omitted one.** Rung 7 returns
  `FindRecord`'s `null` and the property is set to `null` — a typo'd foreign key produces a
  successfully-applied record with a silently empty reference, no exception.
- **Bare-word arrays are all-or-nothing on quoting.** `[a,b]` works and `['a','b']` works, but
  `[a,'b']` contains a `'`, so the bare-word branch is skipped and the single→double replacement
  runs — producing `[a,"b"]`, which is invalid JSON and throws. Do not mix.
- **Mixed quote styles fail the same way.** The single→double replacement only runs when no `"` is
  present, so `['a',"b"]` is handed to `System.Text.Json` unchanged and throws.
- **An empty string means `null` for a nullable, but `""` for a `string`.** Rung 1 fires before
  rung 2, and `string` is a reference type that `Nullable.GetUnderlyingType` does not unwrap — so
  `--Name=""` sets an empty string while `--Count=""` sets `null`.
- **`Register` overwrites the type description for a record type** on every call, formatting it as
  `string <record-uniquekey>`; customizing it is an open TODO.

## Executable references

[`RecordCommander.Tests/RecordCommanderTests.cs`](../RecordCommander.Tests/RecordCommanderTests.cs).
Where prose here and a test disagree, **the test wins.**

- The ladder across types (the authority): `Arguments_VariousTypes`,
  `Arguments_Should_HandleNullableValueTypes`, `Arguments_Should_HandleEmptyString`.
- Custom converters, including the override path: `CustomConversion_Unit`,
  `CustomConversion_Country`.
- Array normalization of all three forms: `AddCountry_SpokenLanguages_SingleQuotedInlineArray`,
  `…_DoubleQuotedInlineArray`, `…_FullyQuotedArrayValue`, `…_ElementContainingSpaces`,
  `InvalidArrayFormat_ShouldThrowException`.
- Invariant-culture behavior: `ParseWithDifferentCultures_ShouldHandleCultureCorrectly`.
- Enums, including `[Flags]`: `AddBook_ValidInput_Enums`.
- Record-typed properties resolving by key: `ComplexObjectHierarchy_ShouldHandleNestedObjects`,
  `Generation_UsingRelatedRecord`.

**Unasserted, and the riskiest thing to change:** no test pins the *silent `null`* for an unresolved
related record, and none covers the mixed-quote array forms described above. The netstandard2.0
`DateOnly` gap is unreachable by the suite — the test project targets `net10.0` only, so rungs
guarded by `#if NET8_0_OR_GREATER` are compiled for the other targets but never executed.

## Links

- Glossary: [`UBIQUITOUS_LANGUAGE.md`](../UBIQUITOUS_LANGUAGE.md) §
  [Conversion](../UBIQUITOUS_LANGUAGE.md#conversion) ·
  [Flagged ambiguities](../UBIQUITOUS_LANGUAGE.md#flagged-ambiguities) (notably *parse* vs
  *tokenize*)
- Related domains: [`commands`](commands.md) (boundary: token boundaries and member resolution
  happen there; this domain only sees `(value, targetType)`) · [`generation`](generation.md)
  (boundary: the reverse direction, and not a round-trip guarantee)
- Priming skill: [`.claude/skills/conversion/SKILL.md`](../.claude/skills/conversion/SKILL.md)
