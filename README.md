# RecordCommander

RecordCommander is a C# library that enables you to create, update, and manage records using a command-line–inspired interface. With a focus on simplicity and flexibility, RecordCommander allows you to register your data types, define unique keys and property mappings, and use both positional and named arguments to seed data or integrate with import/export systems.

## Features

- **Command-Based Record Management:** Create or update records using single-line commands (e.g., `add language nl Dutch`).
- **Flexible Configuration:** Register classes by specifying unique keys, positional property order, and named property assignments.
- **Data Seeding & Import/Export:** Ideal for seeding data, providing sample data for documentation, or integrating with external systems.
- **JSON–Like Parsing:** Supports complex property values (including arrays) with JSON–like syntax.
- **No External Dependencies:** Uses only the default Microsoft libraries.

## Getting Started

### Installation

Simply clone the repository and include the source code in your project. Since RecordCommander relies solely on standard .NET libraries, no additional NuGet packages are required.

### Configuration

Before running commands, register your record types with the library. For example, suppose you have the following domain classes:

```csharp
public class MyData
{
    public List<Language> Languages { get; set; } = new List<Language>();
    public List<Country> Countries { get; set; } = new List<Country>();
}

public class Language
{
    public string Key { get; set; }
    public string Name { get; set; }
}

public class Country
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string[] SpokenLanguages { get; set; } = Array.Empty<string>();
}
```

You would register them as follows:

```csharp
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using RecordCommander; // Use your namespace

// Register the Language record.
RecordCommandRegistry.Register<MyData, Language>(
    commandName: "language",
    collectionAccessor: ctx => ctx.Languages,
    uniqueKeySelector: x => x.Key,
    positionalPropertySelectors: new Expression<Func<Language, object>>[] { x => x.Name }
);

// Register the Country record.
RecordCommandRegistry.Register<MyData, Country>(
    commandName: "country",
    collectionAccessor: ctx => ctx.Countries,
    uniqueKeySelector: x => x.Code,
    positionalPropertySelectors: new Expression<Func<Country, object>>[] { x => x.Name, x => x.SpokenLanguages }
);
```

### Running Commands

After registration, you can execute commands that create or update records in your data context. For example:

```csharp
// Create a context instance.
var context = new MyData();

// Run commands to seed or update data.
RecordCommandRegistry.Run(context, "add language nl Dutch");
RecordCommandRegistry.Run(context, "add language fr French");
RecordCommandRegistry.Run(context, "add country be Belgium");
// Update an existing record using named arguments:
RecordCommandRegistry.Run(context, "add country be --SpokenLanguages=['nl','fr']");
```

### Example Output

After executing the commands above, you might see output like:

```
Languages:
Key: nl, Name: Dutch
Key: fr, Name: French

Countries:
Code: be, Name: Belgium, SpokenLanguages: nl, fr
```

## How It Works

1. **Registration:**  
   You register each record type with a command name, a lambda to access the target collection from your context, and expressions that define the unique key and positional properties.

2. **Command Parsing:**  
   The `Run` method tokenizes the input command. It supports quoted strings, simple escape sequences, and recognizes tokens prefixed with `--` as named property assignments.

3. **Record Updating/Creation:**  
   The library searches for an existing record using the unique key. If found, it updates the record’s properties; otherwise, it creates a new record and adds it to the collection.

4. **Type Conversion:**  
   Values are converted to the target property types using built-in conversion mechanisms. Array values can be provided in a JSON–like syntax for easy parsing.

## Contributing

Contributions are welcome! If you have suggestions or improvements, please open an issue or submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE).
