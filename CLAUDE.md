# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

- **Build**: `dotnet build`
- **Test**: `dotnet test`
- **Run specific test**: `dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"`
- **Package creation**: `dotnet pack` (automatically happens during build due to GeneratePackageOnBuild=true)

## High-Level Architecture

RecordCommander is a lightweight C# library for command-based record management. The architecture consists of:

### Core Components

1. **RecordCommandRegistry** (RecordCommander/RecordCommandRegistry.cs)
   - Static and generic versions for command registration and execution
   - Handles command parsing, tokenization, and type conversion
   - Supports both positional and named arguments

2. **RecordRegistration** (RecordCommander/RecordRegistration´1.cs and RecordRegistration´2.cs)
   - Stores registration metadata for each record type
   - Two versions: one for standard collections, one for custom find/create logic

3. **Command Generation** (RecordCommander/RecordCommandRegistry.Generation.cs)
   - Generates commands from existing records
   - Supports CommandGenerationOptions for customizing output format

4. **AliasAttribute** (RecordCommander/AliasAttribute.cs)
   - Allows alternative names for classes and properties in commands

### Key Design Patterns

- **Expression Trees**: Used extensively for property selection and dynamic compilation
- **Reflection**: For property discovery and type conversion
- **Command Pattern**: Each command is parsed and executed against a context
- **Registry Pattern**: Central registration of record types and their configurations

### Multi-Targeting

The library targets multiple frameworks:
- .NET 9.0
- .NET 8.0  
- .NET Standard 2.0 (includes System.Text.Json dependency)

### Testing

Tests use xUnit and are located in RecordCommander.Tests. The test project provides comprehensive coverage of:
- Command parsing and tokenization
- Record creation and updates
- Complex property types (arrays, enums, flags)
- Alias resolution
- Command generation
- Custom converters and method mapping