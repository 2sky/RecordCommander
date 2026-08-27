# RecordCommander — documentation

This folder holds the **per-domain documentation layer**: one deep living spec per domain, each
paired with a thin agent-priming skill under `.claude/skills/`. The specs capture *current state* —
entities, invariants, key files, gotchas — not the history of any one change.

For build commands, test tooling, and repo-wide conventions see [`../CLAUDE.md`](../CLAUDE.md) and
[`../AGENTS.md`](../AGENTS.md). For user-facing feature documentation see
[`../README.md`](../README.md), which also ships inside the NuGet package.

## Domain index

| Domain | Living spec | Priming skill | Governing issues |
|---|---|---|---|
| Commands | [`commands.md`](commands.md) | [`.claude/skills/commands/SKILL.md`](../.claude/skills/commands/SKILL.md) | — foundational |
| Conversion | [`conversion.md`](conversion.md) | [`.claude/skills/conversion/SKILL.md`](../.claude/skills/conversion/SKILL.md) | — foundational |
| Generation | [`generation.md`](generation.md) | [`.claude/skills/generation/SKILL.md`](../.claude/skills/generation/SKILL.md) | — foundational |

The split follows the data flow, and matches how the code actually changes: `commands` and
`conversion` are the inbound path (a string becomes a mutated record), `generation` is the outbound
one (a record becomes a string). `RecordCommandRegistry.Generation.cs` changes on its own in roughly
half of all library commits, which is why generation is its own domain rather than an appendix to
parsing.

**Deliberately not domains** — these are technical layers, and live where they already are:

- Build and test tooling (the `global.json` Microsoft.Testing.Platform opt-in and the VSTest trap) —
  [`../CLAUDE.md`](../CLAUDE.md).
- The `netstandard2.0` multi-targeting discipline (`#if NET8_0_OR_GREATER` pairs) —
  [`../CLAUDE.md`](../CLAUDE.md).
- Code style (indentation, file-scoped namespaces, XML docs) — [`../AGENTS.md`](../AGENTS.md).

## Other references

- [`../UBIQUITOUS_LANGUAGE.md`](../UBIQUITOUS_LANGUAGE.md) — canonical glossary; the specs link
  *down* into it rather than redefining terms.
- [`../CLAUDE.md`](../CLAUDE.md) — repo guidance (build, conventions, doc-sync rules).
- [`../CHANGELOG.md`](../CHANGELOG.md) — release history; the release process is in `../CLAUDE.md`.

There are currently no per-issue PRDs and no recipe skills in this repo. PRDs, when added, belong
here as `docs/issue_<n>_PRD.md`; the living specs sit *above* them.

## Adding a new domain

Each domain gets a hybrid pair, split by audience: a deep human-facing living spec at
`docs/<domain>.md`, and a thin agent-facing priming skill at `.claude/skills/<domain>/SKILL.md` that
links *down* to the spec. Lowercase single-word filenames.

**Living-spec sections:** title + purpose · status / governing issues · what it is · core entities &
relationships · invariants & rules · key files · gotchas · executable references · links.

**Priming-skill shape:** frontmatter (`name` matching the directory, plus a `description` carrying
concrete entity names, trigger phrases, and an explicit `Not for X` exclusion) → one line on what it
is + link to the spec → get-these-right invariants → key files → gotchas.

**Executable references are required.** Name the tests that pin each invariant and say which is the
authority for which rule. Where a spec's prose and a test disagree, **the test wins** — fix the prose
in the same pass. Say so plainly when a behavior has no test, and name the riskiest unasserted one.

**Same-PR sync rule:** any change to a domain's behavior updates its living spec in the same PR as
the code change — never as a follow-up. If it alters a load-bearing invariant, update the priming
skill too. A domain-behavior diff with no matching spec edit is incomplete.

**The iron rule: the skill links, never duplicates.** If content is more than a compact essential, it
belongs in the spec and the skill points at it.
