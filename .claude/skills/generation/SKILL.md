---
name: generation
description: Prime on RecordCommander's command-emission domain — GenerateCommand, GetUsageExample, GetDetailedUsageExample, GetCustomCommandPrompt, CommandGenerationOptions, [DefaultValue] handling, and the deliberate generate→parse asymmetry. Use when the task touches turning a record back into an `add` line, usage examples or LLM prompts, type descriptions, alias preference, positional-vs-named output, or round-trip fidelity. Not for parsing and applying commands (see commands) or the string→CLR ladder (see conversion).
---

# Generation domain — priming

**Canonical spec:** [`docs/generation.md`](../../../docs/generation.md) — read it for the full
invariant list, every known round-trip break, and which tests pin what. Terms of record:
[`UBIQUITOUS_LANGUAGE.md`](../../../UBIQUITOUS_LANGUAGE.md).

The output half: reads the same `RecordRegistration` metadata `commands` builds and emits command
text or a usage shape. It is **not** the mirror image of parsing — the asymmetry is deliberate and
partly unfinished.

## Core invariants (get these right)

- **generate→parse is not a round trip.** `ConvertValueToString` does not escape embedded quotes
  (open TODO), quotes spaced array elements into `[a,"b c"]` — which `conversion` then rejects as
  invalid JSON — and handles no collection type but arrays. Only `Generation_Books_Roundtrip`'s
  narrow case (strings + `int`, no arrays, no quotes) is guaranteed.
- **Positional emission stops at the first missing positional**; every remaining property, later
  positionals included, becomes a `--named` argument. That is what keeps a partial record generating
  a runnable command.
- **`null` properties are always skipped,** regardless of `IgnoreDefaultValues`.
- **`[DefaultValue]` wins over `default(T)`.** A non-null reference type with no attribute is never
  treated as default.
- **A registered record-typed value is emitted as its unique key**, not its `ToString()`.
- **The unique key placeholder never honours an alias** in `GetUsageExample`, and `filterProperty`
  cannot exclude the key — every other property honours both.

## Key files / reuse

- `RecordCommander/RecordCommandRegistry.Generation.cs` — all four entry points plus its **own**
  `file static class Helpers` (`IsDefaultValue`, `ConvertValueToString`, `GetAlias`,
  `GetTypeDescription`), disjoint from the same-named class in `RecordCommandRegistry.cs`.
- `RecordCommander/CommandGenerationOptions.cs` — the three flags and the static mutable `Default`.

## Gotchas

- **`CommandGenerationOptions.Default` is process-global mutable state** with a public setter.
- **An aliased property enters the candidate set twice** (`AllProperties` holds name *and* alias keys)
  and collapses only because the `HashSet` tuple compares equal — dedupe is by value, not by property
  identity.
- **Named-argument order follows `HashSet` enumeration, not registration order.** The exact-string
  assertions in the tests are the de-facto pin on it.
- `GetTypeDescription` does not expand `[Flags]` enums and covers only `int`/`long`/`decimal`/
  `float`/`double` among numerics (both open TODOs).
