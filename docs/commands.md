# Commands

Registration of record types, and the parse-and-apply path that turns a command string into a
mutated record on a caller-supplied context.

**Status:** current as of `f03195e` (`fix: keep quoted and bracketed values inside a single token`)
· **Governing issues:** no single PRD (foundational domain).
**Priming skill:** [`.claude/skills/commands/SKILL.md`](../.claude/skills/commands/SKILL.md)

## What it is

The input half of the library: `Register` records a type's metadata, `Run` tokenizes one command and
applies it, `RunMany` does that per line. Everything is static state plus reflection — no runtime,
no DI, no I/O.

It is **not** string→CLR coercion: every value conversion is delegated to
[`conversion`](conversion.md), which owns the precedence ladder and its failure modes. It is
**not** command emission — that is [`generation`](generation.md).

## Core entities & relationships

```
RecordCommandRegistry<TContext>   (static, process-global)
  ├─ _registrations   name|alias -> RecordRegistration<TContext>
  ├─ _extraCommands   name       -> CustomCommand
  └─ _customConverters            (owned by `conversion`)

RecordRegistration<TContext>              abstract; reflection metadata
  └─ RecordRegistration<TContext,TRecord> two ctors, two find/create shapes
```

- [`RecordRegistration´1.cs`](../RecordCommander/RecordRegistration´1.cs) — the abstract base. Its
  constructor is where `AllProperties` / `PositionalProperties` / `NonPositionalProperties` are
  reflected out of `TRecord`.
- [`RecordRegistration´2.cs`](../RecordCommander/RecordRegistration´2.cs) — collapses both
  registration shapes into two private delegates (`_findRecord`, `_createRecord`), so `Run` never
  learns which shape the caller used.
- `CustomCommand` — nested private class in
  [`RecordCommandRegistry.cs`](../RecordCommander/RecordCommandRegistry.cs); wraps a `Delegate` and
  computes its arity contract once.

The non-generic `RecordCommandRegistry` is a thin forwarding facade. Real logic belongs in the
generic class.

## Invariants & rules

- **Registration is process-global and irreversible.** `_registrations` is a `static` field on
  `RecordCommandRegistry<TContext>`; there is no unregister, reset, or freeze (open TODO in the
  file). Registering an already-used name **silently overwrites** it for every other consumer in the
  AppDomain. Owned by `RecordCommander/RecordCommandRegistry.cs`.
- **Aliases are not a lookup layer — they are extra keys in `_registrations`.** Class-level
  `[Alias]` and `AddAlias` both insert an additional key pointing at the *same* registration object.
  So an alias that collides with another record's name overwrites that record silently, with no
  error. Owned by `RecordCommandRegistry.cs` (`Register`, `AddAlias`).
- **The unique key must be a `string`.** `_findRecord` compares
  `UniqueKeyProperty.GetValue(item) as string` with `StringComparison.OrdinalIgnoreCase`. A
  non-string key yields `null` from the `as` cast and therefore never matches — every command
  creates a duplicate instead of updating. Unsupported, with an open TODO. Owned by
  `RecordRegistration´2.cs`.
- **Only properties with a public setter are addressable.** The base constructor filters
  `p is { CanWrite: true, SetMethod.IsPublic: true }`. A private-setter property cannot be reached by
  `--Prop=`; expose a 2-parameter method instead. Owned by `RecordRegistration´1.cs`.
- **`add` is reserved.** `RegisterCommand("add", …)` throws. Token 0 is either `add` or a key in
  `_extraCommands`; anything else is `NotSupportedException`. Owned by `RecordCommandRegistry.cs`.
- **A custom command's first parameter must be `TContext`,** validated at registration.
  `RequiredParams` is the *leading run* of non-defaulted parameters — so defaults are only effective
  when they are trailing. Owned by `CustomCommand`.
- **`add` is an upsert, never a lookup failure.** `FindOrCreateRecord` creates on miss. There is no
  command shape that means "update only if present".

## Command grammar

```
add <record> <uniqueKey> [positional...] [--Prop=value] [--Method:arg=value]
```

Resolution order for each `--` token, in `Run`: exact/alias/case-insensitive hit in `AllProperties`
→ else, if the key contains a `:` at index > 0, a method call → else throw.

- `--Name:arg=value` matches a method named `Name` **or** `Set` + `Name`, requires exactly 2
  parameters, and prefers the exact-name match over the `Set`-prefixed one. Both the key-side arg
  and the value are converted.
- `RunMany` splits on `\r\n`/`\n`, trims, and skips blank lines and lines starting with `#`.

### Tokenizer contract

[`Helpers.Tokenize`](../RecordCommander/RecordCommandRegistry.cs) — a `file static class`, so it is
invisible to `RecordCommandRegistry.Generation.cs`, which declares its own same-named `Helpers`.

- **Quotes protect whitespace but never terminate a token.** Adjacent quoted sections concatenate
  into one token, as a shell would do. Both `'` and `"` open a quoted section, and a backslash
  escapes the next character *inside* one.
- **Whitespace inside `[...]` is preserved** via a bracket-depth counter, which is what makes
  `--SpokenLanguages=["fi", "sv"]` survive as one token.
- **A token that contained a quoted section is emitted even when empty,** tracked by the `quoted`
  flag — that is how `--Name=""` reaches `Run` at all.

Both behaviors are load-bearing for the README's "paste AI output" use case: before they existed,
every inline-quoted array threw.

## Key files

| File | Role |
|---|---|
| [`RecordCommander/RecordCommandRegistry.cs`](../RecordCommander/RecordCommandRegistry.cs) | Registration, `Run`/`RunMany`, `CustomCommand`, `Tokenize`, and `ConvertToType` (see `conversion`) |
| [`RecordCommander/RecordRegistration´1.cs`](../RecordCommander/RecordRegistration´1.cs) | Abstract base; property reflection and alias expansion |
| [`RecordCommander/RecordRegistration´2.cs`](../RecordCommander/RecordRegistration´2.cs) | Both registration shapes → two delegates |
| [`RecordCommander/AliasAttribute.cs`](../RecordCommander/AliasAttribute.cs) | `[Alias]`, valid on classes and properties, `AllowMultiple = true` |

## Gotchas

- **Surplus positional arguments are silently ignored.** The apply loop is bounded by
  `min(PositionalProperties.Count, positionalArgs.Count)` — a typo'd extra positional produces no
  error and no effect.
- **A skipped middle positional shifts every later one.** Positionals fill in registration order
  with no placeholder syntax; the result is a successfully-applied, silently-wrong record. Use named
  arguments when any positional is absent.
- **`--Prop` without `=` throws, but `--:foo=bar` does not become a method call** — the colon must be
  at index > 0, so a leading-colon key falls through to "property does not exist".
- **Registration order matters at run time, not at registration time.** A property typed as another
  registered record resolves through `FindRecord`, so the referenced type must be registered by the
  time the command runs — not when the property's owner was registered.

## Executable references

[`RecordCommander.Tests/RecordCommanderTests.cs`](../RecordCommander.Tests/RecordCommanderTests.cs)
— 65 tests, the whole suite, one file. Where prose here and a test disagree, **the test wins.**

- Tokenizer contract (the authority): `AddCountry_SpokenLanguages_SingleQuotedInlineArray`,
  `…_DoubleQuotedInlineArray`, `…_FullyQuotedArrayValue`, `…_ElementContainingSpaces`,
  `AdjacentQuotedSections_FormASingleToken`, `EmptyQuotedArgument_IsPreservedAsToken`,
  `RunMany_AiGeneratedCountryList_WithSpacedInlineArrays`.
- Aliases resolving to the same registration: `AddLanguage_…_ViaAlias`, `…_ViaAlias2`,
  `UpdateLanguage_ShouldUpdateExistingRecord_ViaRunManyAndAlias`.
- Method mapping and its arity rules: `MethodMapping_ShouldCallCorrectMethod`,
  `MissingMethod_ShouldThrowException`, `InvalidMethodArguments_ShouldThrowException`,
  `InvalidMethodCallFormat_ShouldThrowException`.
- Custom-command arity and trailing defaults: `CustomCommands_WithOptionalParameters`,
  `CustomCommands_Log2_WithOptionalParameters`,
  `InvalidMethodArguments_CustomCommands_{NotEnoughParameters,TooManyParameters,Log}`,
  `CantRegisterReservedCommand`.
- `RunMany` line handling:
  `UpdateLanguage_ShouldUpdateExistingRecord_ViaRunMany_AndIgnoreEmptyOrCommentLines`.
- The find/create registration shape: `AddBook_ValidInput` — `TestContext.Books` is an
  `ICollection`, so it cannot use the `IList` collection-accessor shape.

**Unasserted, and the riskiest thing to change:** nothing covers re-registering an existing name, an
alias colliding with a record name, or a non-string unique key. The static registry has no teardown,
so such a test would have to introduce its own context type — which is also the rule for any new
test that needs different registration metadata.

## Links

- Glossary: [`UBIQUITOUS_LANGUAGE.md`](../UBIQUITOUS_LANGUAGE.md) §
  [Registration & context](../UBIQUITOUS_LANGUAGE.md#registration--context) ·
  [Command grammar](../UBIQUITOUS_LANGUAGE.md#command-grammar) ·
  [Flagged ambiguities](../UBIQUITOUS_LANGUAGE.md#flagged-ambiguities) (notably *command*, *alias*,
  and *Helpers*)
- Related domains: [`conversion`](conversion.md) (boundary: `commands` decides *which* property or
  parameter receives a value; `conversion` decides what that string becomes) ·
  [`generation`](generation.md) (boundary: the inverse direction, and deliberately not symmetric)
- Priming skill: [`.claude/skills/commands/SKILL.md`](../.claude/skills/commands/SKILL.md)
