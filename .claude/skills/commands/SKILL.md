---
name: commands
description: Prime on RecordCommander's command-registration and parse-apply domain — the static per-TContext registry, RecordRegistration metadata, the Tokenize contract, the `add <record> <key> [positional] [--Prop=value] [--Method:arg=value]` grammar, RunMany, and custom commands. Use when the task touches Register/AddAlias/RegisterCommand, Run/RunMany, tokenizing or quoting, positional vs named arguments, method mapping, unique keys, or `[Alias]`. Not for string→CLR value coercion (see conversion) or emitting commands and usage examples (see generation).
---

# Commands domain — priming

**Canonical spec:** [`docs/commands.md`](../../../docs/commands.md) — read it for the full
invariant list, the tokenizer contract, and which tests pin what. Terms of record:
[`UBIQUITOUS_LANGUAGE.md`](../../../UBIQUITOUS_LANGUAGE.md).

The input half of the library: registration metadata, then tokenize → dispatch → find-or-create →
apply. All state is static; there is no runtime, DI, or I/O. Value coercion is *not* here — it is
delegated to `conversion` the moment a target type is known.

## Core invariants (get these right)

- **The registry is process-global static per `TContext`, with no unregister or reset.** Registering
  an existing name silently overwrites it for the whole AppDomain. New tests must reuse the existing
  `TestContext` registrations or introduce a *different* context type.
- **Aliases are extra keys in the same `_registrations` dictionary,** pointing at the same
  registration — not a separate lookup layer. Collisions overwrite silently.
- **The unique key must be a `string`,** matched `OrdinalIgnoreCase`. A non-string key never matches,
  so every command creates a duplicate.
- **Only public-setter properties are addressable** by `--Prop=`. Use a 2-parameter method otherwise.
- **`add` is reserved** and is an upsert — `FindOrCreateRecord` creates on miss, so there is no
  "update only if present".
- **Quotes protect whitespace but never terminate a token; `[...]` preserves whitespace via a
  bracket-depth counter.** Both are load-bearing for pasted AI output. Do not "simplify" `Tokenize`.

## Key files / reuse

- `RecordCommander/RecordCommandRegistry.cs` — registration, `Run`/`RunMany`, `CustomCommand`,
  `Tokenize`. Note its `file static class Helpers` is **disjoint** from the same-named class in
  `RecordCommandRegistry.Generation.cs`.
- `RecordCommander/RecordRegistration´1.cs` — property reflection and alias expansion.
- `RecordCommander/RecordRegistration´2.cs` — both registration shapes collapsed to two delegates.
- Put real logic in `RecordCommandRegistry<TContext>`; the non-generic class is a forwarding facade.

## Gotchas

- **Surplus positional args are silently ignored**, and a **skipped middle positional shifts every
  later one** — a silently-wrong record, not an error.
- A custom command's defaults only take effect when **trailing**: `RequiredParams` is the leading run
  of non-defaulted parameters.
- `--Name:arg=value` needs the colon at index > 0, exactly 2 parameters, and prefers the exact-name
  method over `Set` + name.
