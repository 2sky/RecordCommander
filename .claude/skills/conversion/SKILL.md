---
name: conversion
description: Prime on RecordCommander's string→CLR coercion domain — the ConvertToType fallback ladder, RegisterCustomConverter, bracketed-array normalization, nullable/empty-string handling, date and GUID formats, and resolving a property typed as another registered record. Use when the task touches value parsing, custom converters, array syntax, enum or DateTime/DateOnly/TimeSpan/Guid values, InvariantCulture behavior, or type descriptions for prompts. Not for tokenizing and member resolution (see commands) or emitting values back to strings (see generation).
---

# Conversion domain — priming

**Canonical spec:** [`docs/conversion.md`](../../../docs/conversion.md) — read it for the full ladder,
the array-normalization table, and which tests pin what. Terms of record:
[`UBIQUITOUS_LANGUAGE.md`](../../../UBIQUITOUS_LANGUAGE.md).

One private method, `ConvertToType(TContext, string, Type)`, plus the custom-converter registries.
Every value entering the library passes through it exactly once. It never sees token boundaries —
`commands` has already decided those and which member receives the value.

## Core invariants (get these right)

- **The ladder order *is* the specification:** nullable unwrap (empty → `null`) → `string` → array →
  enum → **custom converters** → `DateTime`/`DateOnly`/`TimeSpan`/`Guid` → registered record type →
  `Convert.ChangeType` (InvariantCulture).
- **Custom converters sit *above* the built-in date/GUID rungs** — that ordering is the entire
  mechanism for overriding them. Reordering silently disables caller overrides.
- **Custom converters are keyed by exact `Type`,** not by assignability.
- **Array values must be bracketed**; unbracketed input throws rather than becoming a one-element
  array. Three forms normalize to JSON: `["a","b"]`, `['a','b']`, `[a,b]`.
- **Only `DateTime`/`DateOnly` are format-locked** to exact `yyyy-MM-dd`. `TimeSpan` and `Guid` use
  their permissive `TryParse`.
- **Primitives always convert with `InvariantCulture`** — deliberate, so commands stay portable text.

## Key files / reuse

- `RecordCommander/RecordCommandRegistry.cs` — `ConvertToType`, `RegisterCustomConverter`, and the
  `_customConverters` / `_customConverterTypeDescriptions` dictionaries.
- `RecordCommander/RecordCommandRegistry.Generation.cs` — `Helpers.GetTypeDescription` consumes the
  type descriptions for usage examples and prompts.
- `DateOnly` lives behind `#if NET8_0_OR_GREATER`; match the surrounding `#if` pattern rather than
  raising the target floor.

## Gotchas

- **An unresolved related record yields `null`, silently.** A typo'd foreign key produces a
  successfully-applied record with an empty reference and no exception.
- **Never mix quote styles in an array.** `[a,b]` and `['a','b']` work; `[a,'b']` and `['a',"b"]` both
  reach `System.Text.Json` as invalid JSON and throw.
- **Empty string means `null` for a nullable but `""` for a `string`** — the nullable rung fires
  first, and `string` is not unwrapped by `Nullable.GetUnderlyingType`.
- The test project targets `net10.0` only, so `netstandard2.0` rungs compile but never execute.
