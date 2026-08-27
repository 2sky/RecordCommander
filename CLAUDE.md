# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

- **Build**: `dotnet build`
- **Test**: `dotnet test`
- **Run single test**: `dotnet test --filter "FullyQualifiedName~RecordCommanderTests.AddLanguage_ValidInput_ShouldCreateLanguageRecord"`
- **Package**: `dotnet pack` (also happens on every build — `GeneratePackageOnBuild=true`)

Notes:
- The test project targets **net9.0 only**, so `dotnet test` never exercises the `netstandard2.0` code paths even though `dotnet build` compiles them.
- The `netstandard2.0` build emits a pre-existing `CS8601` warning at `RecordCommander/RecordCommandRegistry.cs:310` — not introduced by your change.

## Architecture

A command string like `add country be Belgium --SpokenLanguages=['nl','fr']` is tokenized, routed to a registration, and applied to a record found-or-created inside a caller-supplied context object. There is no runtime, no DI, no I/O — everything is static state plus reflection.

### One static registry per TContext

`RecordCommandRegistry<TContext>` (`static partial`) holds *all* state in static fields: `_registrations`, `_extraCommands`, `_customConverters`, `_customConverterTypeDescriptions`. Registration is process-global for the lifetime of the AppDomain and there is **no unregister or reset**.

This shapes the tests: `RecordCommanderTests` does every registration in a static constructor guarded by `IsRegistered("language")` plus per-block `try/catch`. New tests must reuse the existing `TestContext` registrations or introduce a *different* context type — re-registering with different metadata silently overwrites for every other test.

The non-generic `RecordCommandRegistry` is a thin forwarding facade (`Register`/`Run`/`RunMany`) over the generic one. Put real logic in `RecordCommandRegistry<TContext>`.

### Partial-class split, and two same-named Helpers

- `RecordCommandRegistry.cs` — registration, `Run`/`RunMany`, `ConvertToType`, nested `CustomCommand`
- `RecordCommandRegistry.Generation.cs` — `GenerateCommand`, `GetUsageExample`/`GetDetailedUsageExample`, `GetCustomCommandPrompt`

Each file declares its **own** `file static class Helpers` with disjoint members (tokenizing/expression parsing in the first, alias/default-value/stringification in the second). They are not visible to each other; adding a helper means choosing a file, not extending a shared class.

### Two registration shapes, one registration type

`RecordRegistration<TContext, TRecord>` accepts either:
1. `collectionAccessor: ctx => ctx.Languages` (`IList<TRecord>`) — find by linear scan, create via `Activator.CreateInstance` + `collection.Add`
2. a `findRecord`/`createRecord` delegate pair — for records not held in a plain list (see `TestContext.Books`, an `ICollection`)

Both collapse into two private delegates, so `Run` never knows which mode was used. The abstract base `RecordRegistration<TContext>` holds the reflection metadata: `UniqueKeyProperty`, `PositionalProperties`, `NonPositionalProperties`, and `AllProperties` (case-insensitive, includes property `[Alias]` names).

The unique key must be a `string` and is matched `OrdinalIgnoreCase`. Non-string keys are unsupported (see TODO in `RecordRegistration´2.cs`).

Class-level `[Alias]` and `AddAlias` add *extra keys into the same `_registrations` dictionary* pointing at the same registration — there is no separate alias lookup layer.

### Command grammar

```
add <record> <uniqueKey> [positional...] [--Prop=value] [--Method:arg=value]
```

- Token 0 other than `add` is looked up in `_extraCommands` (`RegisterCommand`); `"add"` is a reserved command name.
- `Helpers.Tokenize` handles `'` and `"` quoting with backslash escapes inside quotes; a closing quote always ends the token.
- Positional args fill `PositionalProperties` in order; surplus positional args are silently ignored.
- `--Prop=value` resolves against `AllProperties` (case-insensitive, aliases included).
- `--Name:arg=value` is a **method call**: matches a method named `Name` or `Set` + `Name`, requires exactly 2 parameters, prefers the exact-name match, and converts both the key-side arg and the value.
- `RunMany` splits on newlines, skipping blank lines and lines starting with `#`.
- Custom commands (`RegisterCommand`) require `TContext` as the first parameter; trailing parameters may have defaults, and `CustomCommand` computes `RequiredParams` as the leading run of non-defaulted parameters.

### ConvertToType fallback order (load-bearing)

nullable unwrap (empty string → `null`) → `string` → array → enum → **custom converters** → `DateTime`/`DateOnly` (exact `yyyy-MM-dd`) / `TimeSpan` / `Guid` → registered record type → `Convert.ChangeType` (InvariantCulture).

Consequences worth knowing before editing:
- Custom converters are consulted **before** the built-in date/guid handling, which is how a caller overrides them.
- Array values must be bracketed; three forms are normalized to JSON before `System.Text.Json` deserializes: `["a","b"]`, `['a','b']`, and bare `[a,b]`. Unbracketed input throws.
- A property typed as another *registered* record resolves via `FindRecord` on the unique key and yields `null` when absent (no create, no error).

### Generation is the inverse — and deliberately asymmetric

`GenerateCommand` walks `AllProperties`, skips nulls and defaults (`[DefaultValue]` wins over `default(T)`), maps registered record-typed values back to their unique key, emits positionals in registration order but **stops at the first missing one** (remaining properties become `--named` args).

`Helpers.ConvertValueToString` quotes only when the value contains a space and does **not** escape embedded quotes (TODO in code), so round-tripping generate→parse is not guaranteed for such values. `GetTypeDescription` also does not yet expand `[Flags]` enums.

### netstandard2.0 constraint

Modern APIs need an `#if NET8_0_OR_GREATER` / `#else` pair — the file is full of them: `ArgumentNullException.ThrowIfNull`, range/index syntax, `StartsWith(char)`, `Enum.TryParse(Type, ...)`, `DateOnly`, `string.Join(char, ...)`. Match the surrounding pattern rather than raising the floor. `netstandard2.0` also pulls in `System.Text.Json` as a package reference; the other targets use the built-in one.

## Conventions

- Code style lives in `AGENTS.md` (4-space indent, file-scoped namespaces, XML docs on public APIs). Run `dotnet test` after changes.
- Commits use **Conventional Commits** (`feat:`, `fix:`, `docs:`, `test:`, `build:`, `chore:`, `refactor:`).
- `README.md` is the user-facing feature documentation *and* is packed into the NuGet package — update it when adding or changing a public feature.

### Release process

1. Bump `<Version>` in `RecordCommander/RecordCommander.csproj`
2. Add the release section to `CHANGELOG.md` (grouped: Added / Changed / Fixed / Documentation / Tests / Dependencies)
3. Commit as `build: vX.Y.Z`
4. Tag that commit `vX.Y.Z`
