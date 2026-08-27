# Ubiquitous Language

The agreed vocabulary for RecordCommander. The per-domain living specs in [`docs/`](docs/README.md)
link *down* into these sections rather than redefining terms.

## Registration & context

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **Context** | The caller-supplied object that owns every record the library reads or mutates; the library holds no storage of its own. | Container, database, store, session |
| **Record** | One instance of a registered type, identified within its context by a unique key. | Entity, row, model, item |
| **Registration** | The metadata that makes a record type addressable by commands: its name, unique key, positional properties, and how to find or create an instance. | Mapping, config, definition |
| **Registry** | The static, process-global set of registrations, custom commands, and custom converters held per context type. | Catalog, index |
| **Unique key** | The single `string` property that identifies a record within its context, matched case-insensitively. | Id, primary key, identifier |
| **Record name** | The token that selects a registration in a command; defaults to the record type's name and is case-insensitive. | Command name, type name |
| **Positional property** | A property that may be supplied by position, in registration order, after the unique key. | Ordered argument, index property |
| **Non-positional property** | A settable property that is neither the unique key nor positional, and so is reachable only by name. | Optional property, extra property |

## Command grammar

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **Command** | One line of text that the library parses and applies — either an `add` command or a custom command. | Statement, instruction, script |
| **`add` command** | The built-in upsert command: it finds the record by unique key or creates it, then applies the remaining arguments. | Insert, create, upsert command |
| **Custom command** | A caller-registered verb other than `add`, backed by a delegate whose first parameter is the context. | Extra command, action, hook |
| **Token** | One unit of a command after tokenizing; quotes protect whitespace inside a token but never end it. | Word, argument, field |
| **Positional argument** | A token that fills the next positional property by position. | Ordered arg, bare arg |
| **Named argument** | A `--Property=value` token, resolved case-insensitively against property names and property aliases. | Flag, option, switch |
| **Method mapping** | A `--Name:arg=value` token that invokes a two-parameter method named `Name` or `Set` + `Name` on the record. | Method call arg, setter arg |
| **Alias** | An alternative name for a record type or a property, declared by `[Alias]` or `AddAlias`. | Shorthand, nickname, synonym |

## Conversion

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **Conversion** | Turning one command value, always a string, into the CLR type its target expects. | Parsing, casting, binding |
| **Fallback ladder** | The fixed, ordered sequence of conversion attempts; its order is part of the library's contract, not an implementation detail. | Fallback chain, type switch, precedence list |
| **Custom converter** | A caller-registered function that converts strings to one exact CLR type, consulted above the built-in date, time, and GUID handling. | Type handler, parser, formatter |
| **Type description** | The human- and LLM-readable phrase describing what a property or parameter accepts, used only when generating examples and prompts. | Type hint, schema, docstring |
| **Related record** | A property whose type is itself a registered record type, supplied in a command as that record's unique key. | Reference, foreign key, link |

## Generation

| Term | Definition | Aliases to avoid |
| --- | --- | --- |
| **Generation** | Producing command text from a live record, or a command shape from a registration. | Serialization, export, rendering |
| **Generation options** | The three flags that control emitted output: alias preference, positional versus named arguments, and whether default values are omitted. | Settings, config, flags |
| **Default value** | The value at which a property is omitted from generated output — a `[DefaultValue]` attribute when present, otherwise `default(T)`. | Initial value, fallback, empty value |
| **Usage example** | A one-line command *shape* with `<placeholder>` slots, generated from a registration rather than from any instance. | Template, sample, signature |
| **Custom command prompt** | The generated shape of a custom command, with optional trailing parameters shown in brackets. | Signature, help text |
| **Round trip** | Generating a command from a record and parsing it back to an equal record — supported only for the narrow case the tests pin, never a general guarantee. | Reversible, symmetric, lossless |

## Relationships

- A **registry** exists per context *type*, is process-global, and cannot be reset or unregistered.
- A **registration** belongs to exactly one record type, but may be reachable under several
  **record names** — its own plus one key per **alias**, all pointing at the same registration.
- A **record** is found or created inside exactly one **context**; the library never stores it.
- An **`add` command** carries exactly one **unique key**, then zero or more **positional
  arguments**, **named arguments**, and **method mappings**, in any order after the key.
- Every **positional argument**, **named argument**, **method mapping** argument, and **custom
  command** parameter passes through **conversion** exactly once.
- A **related record** resolves to an existing **record** or to nothing; it is never created by the
  command that references it.
- **Generation** reads the same **registration** as parsing, but is a separate implementation — so a
  **round trip** holds only where a test says it does.

## Example dialogue

> **Dev:** "If I run `add country be Belgium` twice, do I get two **records**?"

> **Domain expert:** "No — `add` is an upsert. It matches the **unique key** case-insensitively
> inside the **context**, so the second command updates the first **record**."

> **Dev:** "And `add ctr be …` — is `ctr` a separate **registration**?"

> **Domain expert:** "It's an **alias**. Aliases aren't a lookup layer; they're extra **record
> names** in the same registry pointing at the same **registration**. Which is why an alias that
> collides with another record name silently replaces it."

> **Dev:** "What about `--OriginCountry=xx` where no country `xx` exists?"

> **Domain expert:** "That's a **related record**, and the **fallback ladder** resolves it by unique
> key. A miss yields nothing — the property is set to null and no error is raised. It is not created
> for you."

> **Dev:** "So if I **generate** a command from that record afterwards, I get the same text back?"

> **Domain expert:** "Only for the cases the tests pin. **Generation** is a separate implementation
> from parsing, so treat **round trip** as a claim about specific values, not a property of the
> library."

## Flagged ambiguities

- **"Command"** was used for three different things: one line of text, the `add` verb specifically,
  and a caller-registered verb. Use **command** for the line, **`add` command** for the built-in
  verb, and **custom command** for a registered one. Never say "command" when you mean the verb
  token alone — say **record name** for the `add` operand and **custom command** otherwise.
- **"Alias"** applies to both a record type and a property, and the two resolve through different
  dictionaries — record aliases are keys in the registry, property aliases are keys in a
  registration's property map. Qualify as **record alias** or **property alias** whenever both are in
  scope.
- **"Default value"** means two things that disagree: `default(T)` for the CLR type, and the value in
  a `[DefaultValue]` attribute, which *wins* when present. Say **declared default** for the attribute
  and **type default** for `default(T)` when the distinction matters — as it does in generation.
- **"Helpers"** is not one thing. `RecordCommandRegistry.cs` and `RecordCommandRegistry.Generation.cs`
  each declare their own `file static class Helpers` with disjoint members and no mutual visibility.
  Never refer to "the Helpers class" without naming the file; adding a helper means choosing a file.
- **"Parse"** was used both for tokenizing a command and for converting a single value. Reserve
  **tokenize** for splitting text into tokens and **conversion** for turning one value into a CLR
  type; "parse" alone is ambiguous between the two.
- **"Context"** is the caller's data container here, and has nothing to do with `TestContext` as a
  test fixture or with any ambient/async context. It is always the `TContext` type parameter.
