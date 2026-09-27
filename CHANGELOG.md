# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1] - 2026-09-27

### Changed

- Test project migrated to xUnit v3 running on Microsoft.Testing.Platform; `dotnet test` now requires the runner opt-in in `global.json`
- Test and sample projects target .NET 10, and `LangVersion` raised to 14
- `global.json` pins the .NET 10 SDK (`rollForward: latestFeature`), so builds no longer pick up a newer preview SDK

### Removed

- Renovate configuration, which is no longer available on the GitHub organization

### Fixed

- Inline array values are parsed again: `--SpokenLanguages=['nl','fr']` and `--SpokenLanguages=["fi", "sv"]` previously threw, because a closing quote terminated the token and whitespace split it. Quotes now protect whitespace without ending a token, and whitespace inside brackets is preserved, so AI-generated command output runs as pasted.

### Documentation

- Per-domain living specs (`docs/`) for commands, conversion and generation, with matching agent priming skills
- Ubiquitous-language glossary (`UBIQUITOUS_LANGUAGE.md`)
- `CLAUDE.md` rewritten with architecture notes, test commands, tooling traps and coverage

### Tests

- Regression tests for both quote styles, spaced array elements, empty quoted arguments, adjacent quoted sections, and the README's AI-generated command block

### Dependencies

- Updated System.Text.Json to 10.0.12
- Replaced coverlet.collector, Microsoft.NET.Test.Sdk and xunit.runner.visualstudio with xunit.v3 4.0.1 and Microsoft.Testing.Extensions.CodeCoverage 18.11.2

## [1.0.0] - 2025-11-26

### Added

- .NET 10 support

### Dependencies

- Updated System.Text.Json to 10.0.0

## [0.17.0] - 2025-03-05

### Added

- Better support for properties with registered record types
- Support for [DefaultValue] attribute

### Documentation

- Extended features documentation and custom command prompt
- Added SECURITY policy
- Added repository contribution guidelines
- Updated badges with correct links
- Added NuGet installation instructions

### Tests

- Extra test coverage

## [0.16.0] - 2025-02-28

### Added

- Non-generic generation overloads
- Use Description on Property/Parameter

### Fixed

- Handle overloads for method mapping

### Documentation

- Badges and correct logo in NuGet

## [0.15.0] - 2025-02-25

### Added

- Custom converter type descriptions
- Option to ignore properties in examples

### Fixed

- Only work with properties that have a public setter

### Tests

- Parsing of AI generated data based on example

## [0.14.0] - 2025-02-25

### Added

- Include all properties when generating example
- Include README in NuGet package

### Changed

- **BREAKING**: Naming, expose registered, added more tests

## [0.13.0] - 2025-02-25

### Added

- Generation GetCustomCommandPrompt
- Generation GetUsageExample/GetDetailedUsageExample

### Fixed

- Parsing of Date/Time
- Handle nullable value types and empty strings

## [0.12.0] - 2025-02-22

### Added

- Custom converters
- Support optional parameters on RegisterCommand

### Documentation

- Added features

## [0.11.0] - 2025-02-21

### Added

- Extra commands
- Overload for single findOrCreateRecord
- Handle methods with 2 arguments for setting data

### Fixed

- Warnings

## [0.10.0] - 2025-02-05

### Fixed

- Handle enum properties

### Changed

- Ownership

## [0.9.0] - 2025-02-03

### Added

- Allow custom find or create logic
- Multitarget netstandard2.0
- GenerateCommand functionality

### Changed

- **BREAKING**: Made the TContext an explicit part
- Added alias support for commands and properties
- Ignore empty and comment lines on RunMany

### Documentation

- Added Use cases and Limitations

## [0.2.0] - 2025-02-02

### Added

- RunMany option
- Aliases for command names
- Basic tests
- Multitarget .NET 8 and 9
- Logo and NuGet badge

### Dependencies

- Updated xunit to 2.9.3
- Updated coverlet.collector to 6.0.4
- Updated xunit.runner.visualstudio to v3
- Configure Renovate

## [0.1.0] - 2025-02-02

### Added

- Initial version based on Lists
- NuGet package settings

### Documentation

- Initial README.md