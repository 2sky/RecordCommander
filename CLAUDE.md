# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

- **Build**: `dotnet build`
- **Test**: `dotnet test`
- **Run single test**: `dotnet test --filter-method "*AddCountry_SpokenLanguages_SingleQuotedInlineArray"`
- **Coverage**: `dotnet test --coverage --coverage-output-format cobertura --coverage-output cov.cobertura.xml` (report lands in `TestResults/` at the repo root)
- **Package**: `dotnet pack` (also happens on every build — `GeneratePackageOnBuild=true`)

`dotnet test` only works because of the `global.json` at the repository root:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

The test project is xUnit v3 on Microsoft.Testing.Platform **v2**, and MTP v2 removed the old
VSTest bridge: its MSBuild targets hard-error with "Testing with VSTest target is no longer
supported" if `dotnet test` routes through the VSTest target. That opt-in is what selects the
MTP-native `dotnet test` instead, so **deleting `global.json` breaks testing entirely** — and do
not "fix" such a failure by reverting to VSTest or by setting
`TestingPlatformDotnetTestSupport` (the v1-era bridge, which MTP v2 rejects).

MTP options pass straight through `dotnet test` with no `--` separator. Running the test binary
directly also works and takes the same options, which is useful when bypassing MSBuild:
`dotnet run --project RecordCommander.Tests -- --coverage`.

Other notes:
- The test project targets **net10.0 only**, so the `netstandard2.0` code paths are compiled by
  `dotnet build` but never executed by the tests.
- The `netstandard2.0` build emits a pre-existing `CS8601` warning at
  `RecordCommander/RecordCommandRegistry.cs:310` — not introduced by your change.
- Coverage is 92.2% line / 88.1% branch. The weak spot is the non-generic `RecordCommandRegistry`
  facade (45% lines) — its forwarding overloads are mostly unexercised.

## Architecture

A command string like `add country be Belgium --SpokenLanguages=['nl','fr']` is tokenized, routed to
a registration, and applied to a record found-or-created inside a caller-supplied context object.
There is no runtime, no DI, no I/O — everything is static state plus reflection.

Per-domain depth lives in the living specs (see below); these three facts apply to any change:

- **One static registry per `TContext`, with no unregister or reset.** `RecordCommandRegistry<TContext>`
  (`static partial`) holds all state in static fields, process-global for the AppDomain's lifetime.
  This shapes the tests: `RecordCommanderTests` registers everything in a static constructor guarded
  by `IsRegistered("language")` plus per-block `try/catch`. New tests must reuse the existing
  `TestContext` registrations or introduce a *different* context type — re-registering with different
  metadata silently overwrites for every other test.
- **Two same-named `Helpers`.** `RecordCommandRegistry.cs` and `RecordCommandRegistry.Generation.cs`
  each declare their own `file static class Helpers` with disjoint members, invisible to each other.
  Adding a helper means choosing a file, not extending a shared class.
- **netstandard2.0 is a target.** Modern APIs need an `#if NET8_0_OR_GREATER` / `#else` pair — the
  files are full of them: `ArgumentNullException.ThrowIfNull`, range/index syntax,
  `StartsWith(char)`, `Enum.TryParse(Type, ...)`, `DateOnly`, `string.Join(char, ...)`. Match the
  surrounding pattern rather than raising the floor. `netstandard2.0` also pulls in
  `System.Text.Json` as a package reference; the other targets use the built-in one.

The non-generic `RecordCommandRegistry` is a thin forwarding facade over the generic one. Put real
logic in `RecordCommandRegistry<TContext>`.

## Domain Documentation (Living Specs)

Each domain has a **living spec** paired with a **priming skill**, indexed in
[`docs/README.md`](docs/README.md):

- **Living spec** `docs/<domain>.md` — deep, human-facing current-state doc (entities, invariants,
  key files, gotchas, and the tests that pin each rule).
- **Priming skill** `.claude/skills/<domain>/SKILL.md` — thin, agent-facing; loads the essentials
  fast and links *down* to the spec.

Start from the **domain index** in [`docs/README.md`](docs/README.md). The current domains are
[`commands`](docs/commands.md) (registration, tokenizing, the `add` grammar, custom commands),
[`conversion`](docs/conversion.md) (the string→CLR fallback ladder and custom converters), and
[`generation`](docs/generation.md) (emitting commands, usage examples, prompts). Read the relevant
spec **before** changing behavior in its area — the fallback-ladder order and the tokenizer contract
are both load-bearing in ways the code does not announce.

**Same-PR sync rule:** any change to a domain's behavior updates its living spec **in the same PR**
as the code change — never as a follow-up. If the change alters a load-bearing invariant, update the
priming skill too. A domain-behavior diff with no matching spec edit is incomplete.

Auditing and adding domains is handled by the user-level `domain-priming` skill.

## Ubiquitous Language

[`UBIQUITOUS_LANGUAGE.md`](UBIQUITOUS_LANGUAGE.md) is the canonical domain glossary — the agreed
vocabulary for registration, the command grammar, conversion, and generation. Use these terms in
code, comments, XML docs, and the README; consult its "Flagged ambiguities" before naming new
concepts (notably **command** — the line vs. the `add` verb vs. a custom command; **alias** — record
vs. property; **default value** — declared vs. type default; and **Helpers** — never one class).
Update it when introducing or renaming a domain concept.

## Conventions

- Code style lives in `AGENTS.md` (4-space indent, file-scoped namespaces, XML docs on public APIs).
- Commits use **Conventional Commits** (`feat:`, `fix:`, `docs:`, `test:`, `build:`, `chore:`, `refactor:`).
- `README.md` is the user-facing feature documentation *and* is packed into the NuGet package — update it when adding or changing a public feature.

### Release process

1. Bump `<Version>` in `RecordCommander/RecordCommander.csproj`
2. Add the release section to `CHANGELOG.md` (grouped: Added / Changed / Fixed / Documentation / Tests / Dependencies)
3. Commit as `build: vX.Y.Z`
4. Tag that commit `vX.Y.Z`
