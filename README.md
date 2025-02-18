# RecordCommander

![RecordCommander Logo](logo-128x128.png)
![NuGet Version](https://img.shields.io/nuget/v/RecordCommander)

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
using RecordCommander;

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

The AliasAttribute can be used on classes and properties to provide alternative names for the command and properties. For example:
```csharp
[Alias("lang")]
public class Language
{
    public string Key { get; set; }
    [Alias("n")]
    public string Name { get; set; }
}
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
// Or using the aliases:
RecordCommandRegistry.Run(context, "add lang de --n=German");
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
   Likewise the `RunMany` method supports multiple commands in a single string, separated by newlines, ignoring empty lines and lines starting with `#`.

3. **Record Updating/Creation:**  
   The library searches for an existing record using the unique key. If found, it updates the record’s properties; otherwise, it creates a new record and adds it to the collection.

4. **Type Conversion:**  
   Values are converted to the target property types using built-in conversion mechanisms. Array values can be provided in a JSON–like syntax for easy parsing.

## Use Cases

### AI

Once you have a simple set of commands, you can use AI models to let you generate the data for you, for example giving the following propmt to GPT-4o:
```
add country BE "Belgium" --SpokenLanguages=['nl','fr']
add country DE "Germany" --SpokenLanguages=["de"]
add country FR "France" --SpokenLanguages=["fr"]

can you extend the list with all the EU countries in the same format as above
```

It will then generate the following output:
```
add country AT "Austria" --SpokenLanguages=["de"]
add country BE "Belgium" --SpokenLanguages=['nl','fr']
add country BG "Bulgaria" --SpokenLanguages=["bg"]
add country HR "Croatia" --SpokenLanguages=["hr"]
add country CY "Cyprus" --SpokenLanguages=["el"]
add country CZ "Czech Republic" --SpokenLanguages=["cs"]
add country DK "Denmark" --SpokenLanguages=["da"]
add country EE "Estonia" --SpokenLanguages=["et"]
add country FI "Finland" --SpokenLanguages=["fi", "sv"]
add country FR "France" --SpokenLanguages=["fr"]
add country DE "Germany" --SpokenLanguages=["de"]
add country EL "Greece" --SpokenLanguages=["el"]
add country HU "Hungary" --SpokenLanguages=["hu"]
add country IE "Ireland" --SpokenLanguages=["en", "ga"]
add country IT "Italy" --SpokenLanguages=["it"]
add country LV "Latvia" --SpokenLanguages=["lv"]
add country LT "Lithuania" --SpokenLanguages=["lt"]
add country LU "Luxembourg" --SpokenLanguages=["lb", "fr", "de"]
add country MT "Malta" --SpokenLanguages=["mt", "en"]
add country NL "Netherlands" --SpokenLanguages=["nl"]
add country PL "Poland" --SpokenLanguages=["pl"]
add country PT "Portugal" --SpokenLanguages=["pt"]
add country RO "Romania" --SpokenLanguages=["ro"]
add country SK "Slovakia" --SpokenLanguages=["sk"]
add country SI "Slovenia" --SpokenLanguages=["sl"]
add country ES "Spain" --SpokenLanguages=["es"]
add country SE "Sweden" --SpokenLanguages=["sv"]
```

### Dev Data

You can use this library to seed data for your development environment.

### Bug Reproduction

You can use this library to reproduce bugs in your application by creating the same data that caused the bug.

### Documentation

You can use this library to generate sample data for your documentation. So that the user reading your documentation can copy and paste the commands to seed the data.

### Generating commands

You can use this library to generate commands from existing records. Check the following test:

```csharp
[Fact]
public void Generation_UsingSpaces()
{
    var lang = new Language { Key = "en", Name = "English Language" };
    var cmd = RecordCommandRegistry<TestContext>.GenerateCommand(lang);
    Assert.Equal("add language en \"English Language\"", cmd);
}
```

This could be used for exporting data from your application.

## Limitations (at the moment))

- **No Validation:**  
  The library does not perform any validation on the input data. You should validate the data before using it in your application.
- **No Error Handling:**  
  The library does not provide detailed error messages. If a command fails, it will throw an exception with a generic message.
- **No Customization:**  
  The library does not provide customization options for the command parsing or record creation process.

## Contributing

Contributions are welcome! If you have suggestions or improvements, please open an issue or submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE).
