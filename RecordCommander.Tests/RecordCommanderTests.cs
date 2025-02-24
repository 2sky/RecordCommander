// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global

namespace RecordCommander.Tests;

// Sample domain classes for testing
public class TestContext
{
    private readonly List<Book> books = [];

    public List<Language> Languages { get; } = [];
    public List<Country> Countries { get; } = [];

    public ICollection<Book> Books => books;

    public Book? FindBook(string isbn) => books.FirstOrDefault(b => b.ISBN == isbn);

    public Book CreateBook(string isbn)
    {
        var book = new Book { ISBN = isbn };
        books.Add(book);
        return book;
    }

    public List<string> Logs { get; } = [];
}

public record Unit(decimal value, string symbol)
{
    public static Unit Parse(string s)
    {
        var parts = s.Split(' ');
        return new Unit(decimal.Parse(parts[0]), parts[1]);
    }

    public override string ToString() => $"{value} {symbol}";
}

[Alias("lang")]
public class Language
{
    private readonly Dictionary<string, string> labels = [];

    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;

    public void SetLabel(string culture, string label) => labels[culture] = label;

    public string? GetLabel(string culture) => labels.GetValueOrDefault(culture);
}

[Alias("ctr")]
public class Country
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    [Alias("langs")]
    public string[] SpokenLanguages { get; set; } = [];
}

[Alias("bk")]
public class Book
{
    // Unique key for the book.
    public string ISBN { get; set; } = null!;

    // Title of the book.
    public string Title { get; set; } = null!;

    // Author of the book.
    public string Author { get; set; } = null!;

    // Publication year.
    [Alias("year")]
    public int PublicationYear { get; set; }

    // Status of the book.
    public BookStatus Status { get; set; } = BookStatus.Available;

    // Flags for the book.
    public BookFlags Flags { get; set; } = BookFlags.None;

    public Unit? Dimensions { get; set; }

    [Alias("origin-country")]
    public Country? OriginCountry { get; set; }
}

public enum BookStatus
{
    Available,
    Borrowed,
    Lost,
}

[Flags]
public enum BookFlags
{
    None = 0,
    Fiction = 1,
    NonFiction = 2,
    Mystery = 4,
    Thriller = 8,
    Romance = 16,
    Fantasy = 32,
    ScienceFiction = 64,
}

public class RecordCommanderTests
{
    // Static constructor to perform registrations only once.
    // If registration already exists, we catch and ignore the exception.
    static RecordCommanderTests()
    {
        if (RecordCommandRegistry<TestContext>.IsRegistered("language"))
            return;

        try
        {
            RecordCommandRegistry.Register<TestContext, Language>(
                commandName: "language",
                collectionAccessor: ctx => ctx.Languages,
                uniqueKeySelector: x => x.Key,
                positionalPropertySelectors: [x => x.Name]
            );

            // Add an alias for the "language" command.
            RecordCommandRegistry<TestContext>.AddAlias("language", "lang2");
        }
        catch { /* Ignore if already registered */ }

        try
        {
            RecordCommandRegistry.Register<TestContext, Country>(
                commandName: "country",
                collectionAccessor: ctx => ctx.Countries,
                uniqueKeySelector: x => x.Code,
                positionalPropertySelectors: [x => x.Name, x => x.SpokenLanguages]
            );

            // Add an alias for the "country" command.
            RecordCommandRegistry<TestContext>.AddAlias("country", "ctr");
        }
        catch { /* Ignore if already registered */ }

        try
        {
            RecordCommandRegistry.Register<TestContext, Book>(
                commandName: "book",
                findRecord: (ctx, isbn) => ctx.FindBook(isbn),
                createRecord: (ctx, isbn) => ctx.CreateBook(isbn),
                uniqueKeySelector: x => x.ISBN,
                positionalPropertySelectors: [x => x.Title, x => x.Author, x => x.PublicationYear]
            );
        }
        catch { /* Ignore if already registered */ }

        RecordCommandRegistry<TestContext>.RegisterCommand("log", (TestContext context, string log) => context.Logs.Add(log));
        RecordCommandRegistry<TestContext>.RegisterCommand("log2", (TestContext context, string log, bool b = true, int x = 5) => context.Logs.Add($"{log};b={b};x={x}"));
        RecordCommandRegistry<TestContext>.RegisterCommand("log3", (TestContext context, string log, int? x) => context.Logs.Add($"{log};x={x}"));
        // Extra command to test some various types of parameters
        RecordCommandRegistry<TestContext>.RegisterCommand("log4", (TestContext context, string log, DateTime x, DateOnly y, decimal z, TimeSpan ts, Guid g) => context.Logs.Add(FormattableString.Invariant($"{log};x={x};y={y};z={z},ts={ts};g={g}")));
        RecordCommandRegistry<TestContext>.RegisterCommand("add-language-to-country", (TestContext context, string countryCode, string langKey) =>
        {
            var country = context.Countries.FirstOrDefault(c => c.Code == countryCode);
            if (country is null)
                throw new InvalidOperationException($"Country '{countryCode}' not found.");

            var lang = context.Languages.FirstOrDefault(l => l.Key == langKey);
            if (lang is null)
                throw new InvalidOperationException($"Language '{langKey}' not found.");

            if (!country.SpokenLanguages.Contains(langKey))
                country.SpokenLanguages = country.SpokenLanguages.Append(langKey).ToArray();
        });

        // With optional parameters
        RecordCommandRegistry<TestContext>.RegisterCommand("update-language", (TestContext context, string key, string name, string? label = null) =>
        {
            var lang = context.Languages.FirstOrDefault(l => l.Key == key);
            if (lang is null)
            {
                lang = new() { Key = key, Name = name };
                context.Languages.Add(lang);
            }
            if (label is not null)
                lang.SetLabel("en", label);
        });

        // Custom conversion
        RecordCommandRegistry<TestContext>.RegisterCustomConverter((_, s) => Unit.Parse(s));
        RecordCommandRegistry<TestContext>.RegisterCustomConverter((ctx, code) => ctx.Countries.Find(c => c.Code == code));
    }

    [Fact]
    public void AddLanguage_ValidInput_ShouldCreateLanguageRecord()
    {
        var context = new TestContext();
        RecordCommandRegistry.Run(context, "add language en English");

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void AddLanguage_ValidInput_ShouldCreateLanguageRecord_IgnoreCase()
    {
        var context = new TestContext();
        RecordCommandRegistry.Run(context, "add Language en English");

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void AddLanguage_ValidInput_ShouldCreateLanguageRecord_ViaAlias()
    {
        var context = new TestContext();
        RecordCommandRegistry.Run(context, "add lang en English");

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void AddLanguage_ValidInput_ShouldCreateLanguageRecord_ViaAlias2()
    {
        var context = new TestContext();
        RecordCommandRegistry.Run(context, "add lang2 en English");

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void AddCountry_ValidInput_Positional_ShouldCreateCountryRecord()
    {
        var context = new TestContext();
        RecordCommandRegistry.Run(context, "add country us USA");

        Assert.Single(context.Countries);
        var country = context.Countries.First();
        Assert.Equal("us", country.Code, ignoreCase: true);
        Assert.Equal("USA", country.Name);
        Assert.Empty(country.SpokenLanguages);
    }

    [Fact]
    public void AddCountry_SpokenLanguages_ViaAlias()
    {
        var context = new TestContext();
        RecordCommandRegistry.Run(context, "add ctr us USA --langs=[en,es]");

        Assert.Single(context.Countries);
        var country = context.Countries.First();
        Assert.Equal("us", country.Code, ignoreCase: true);
        Assert.Equal("USA", country.Name);
    }

    [Fact]
    public void AddBook_ValidInput()
    {
        var context = new TestContext();
        RecordCommandRegistry.Run(context, "add book 978-3-16-148410-0 \"The Book Title\" \"John Doe\" --year=2021");

        Assert.Single(context.Books);
        var book = context.Books.First();
        Assert.Equal("978-3-16-148410-0", book.ISBN);
        Assert.Equal("The Book Title", book.Title);
        Assert.Equal("John Doe", book.Author);
        Assert.Equal(2021, book.PublicationYear);
    }

    [Fact]
    public void AddBook_ValidInput_Enums()
    {
        var context = new TestContext();
        RecordCommandRegistry.Run(context, "add book 978-3-16-148410-0 \"The Book Title\" \"John Doe\" --year=2021 --status=Borrowed --flags=Fiction,Mystery");

        Assert.Single(context.Books);
        var book = context.Books.First();
        Assert.Equal("978-3-16-148410-0", book.ISBN);
        Assert.Equal("The Book Title", book.Title);
        Assert.Equal("John Doe", book.Author);
        Assert.Equal(2021, book.PublicationYear);
        Assert.Equal(BookStatus.Borrowed, book.Status);
        Assert.Equal(BookFlags.Fiction | BookFlags.Mystery, book.Flags);
    }

    [Fact]
    public void UpdateCountry_WithNamedArgument_ShouldUpdateExistingCountryRecord()
    {
        var context = new TestContext();
        // First, add the country.
        RecordCommandRegistry.Run(context, "add country be Belgium");
        // Then, update it with a named argument for SpokenLanguages.
        RecordCommandRegistry.Run(context, "add country be --SpokenLanguages=[nl,fr]");

        Assert.Single(context.Countries);
        var country = context.Countries.First();
        Assert.Equal("be", country.Code, ignoreCase: true);
        Assert.Equal("Belgium", country.Name);
        Assert.NotNull(country.SpokenLanguages);
        Assert.Equal(2, country.SpokenLanguages.Length);
        Assert.Contains("nl", country.SpokenLanguages);
        Assert.Contains("fr", country.SpokenLanguages);
    }

    [Fact]
    public void UpdateLanguage_ShouldUpdateExistingRecord()
    {
        var context = new TestContext();
        // Add a language record.
        RecordCommandRegistry.Run(context, "add language en English");
        // Update it with a wrong name.
        RecordCommandRegistry.Run(context, "add language en Englsh");
        // Then update it with the correct name.
        RecordCommandRegistry.Run(context, "add language en English");

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void UpdateLanguage_ShouldUpdateExistingRecord_v2()
    {
        var context = new TestContext();
        // Add a language record.
        RecordCommandRegistry.Run(context, "add language en");
        // Update it with a wrong name.
        RecordCommandRegistry.Run(context, "add language en Englsh");
        // Then update it with the correct name.
        RecordCommandRegistry.Run(context, "add language en English");

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void UpdateLanguage_ShouldUpdateExistingRecord_ViaRunMany()
    {
        var context = new TestContext();
        // Add a language record.
        // Update it with a wrong name.
        // Then update it with the correct name.
        RecordCommandRegistry.RunMany(context, """
                                               add language en English
                                               add language en Englsh
                                               add language en English
                                               """);

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void UpdateLanguage_ShouldUpdateExistingRecord_ViaRunMany_AndIgnoreEmptyOrCommentLines()
    {
        var context = new TestContext();
        // Add a language record with a wrong name.
        // Include an empty line and a comment line.
        // Then update it with the correct name.
        RecordCommandRegistry.RunMany(context, """
                                               add language en Englsh

                                               # This is a comment
                                               add language en English
                                               """);

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void UpdateLanguage_ShouldUpdateExistingRecord_ViaRunManyAndAlias()
    {
        var context = new TestContext();
        // Add a language record.
        // Update it with a wrong name.
        // Then update it with the correct name.
        RecordCommandRegistry.RunMany(context, """
                                               add lang en
                                               add lang en Englsh
                                               add lang en English
                                               """);

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
    }

    [Fact]
    public void MethodMapping_ShouldCallCorrectMethod()
    {
        var context = new TestContext();
        // Add a language record.
        RecordCommandRegistry.Run(context, "add language en English");
        // Add label for the language.
        RecordCommandRegistry.Run(context, "add language en --Label:de=Englisch");

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
        Assert.Equal("Englisch", lang.GetLabel("de"));
    }

    [Fact]
    public void InvalidCommand_MissingTokens_ShouldThrowException()
    {
        var context = new TestContext();
        // An empty command should throw an ArgumentException.
        Assert.Throws<ArgumentException>(() => RecordCommandRegistry.Run(context, ""));
    }

    [Fact]
    public void InvalidCommand_UnsupportedAction_ShouldThrowNotSupportedException()
    {
        var context = new TestContext();
        // The "delete" action is not supported.
        Assert.Throws<NotSupportedException>(() => RecordCommandRegistry.Run(context, "delete language en English"));
    }

    [Fact]
    public void InvalidCommand_UnknownRecordType_ShouldThrowException()
    {
        var context = new TestContext();
        // Using an unregistered record type ("unknown") should throw a NotSupportedException.
        Assert.Throws<NotSupportedException>(() => RecordCommandRegistry.Run(context, "add unknown en Something"));
    }

    [Fact]
    public void InvalidNamedArgumentFormat_ShouldThrowException()
    {
        var context = new TestContext();
        // A named argument without '=' should throw an exception.
        var ex = Assert.Throws<ArgumentException>(() => RecordCommandRegistry.Run(context, "add language en --NameEnglish"));
        Assert.Contains("must be in the format --Property=value", ex.Message);
    }

    [Fact]
    public void InvalidArrayFormat_ShouldThrowException()
    {
        var context = new TestContext();
        // Providing an invalid array format (not starting with '[' and ending with ']')
        // should throw an exception.
        var ex = Assert.Throws<ArgumentException>(() =>
            RecordCommandRegistry.Run(context, "add country de Germany --SpokenLanguages=nl;fr")
        );
        Assert.Contains("is not a valid array representation", ex.Message);
    }

    [Fact]
    public void InvalidMethodCallFormat_ShouldThrowException()
    {
        var context = new TestContext();
        // Providing an invalid method call format (missing ':' separator)
        // should throw an exception.
        var ex = Assert.Throws<ArgumentException>(() =>
            RecordCommandRegistry.Run(context, "add language en --Label=de Englisch")
        );
        Assert.Contains("Property 'Label' does not exist on type", ex.Message);

        // Providing an invalid method call format (missing '=' separator)
        // should throw an exception.
        ex = Assert.Throws<ArgumentException>(() =>
            RecordCommandRegistry.Run(context, "add language en --Label:de Englisch")
        );
        Assert.Contains("Named argument", ex.Message);
    }

    [Fact]
    public void MissingMethod_ShouldThrowException()
    {
        var context = new TestContext();
        // Calling a non-existing method should throw an exception.
        var ex = Assert.Throws<ArgumentException>(() =>
            RecordCommandRegistry.Run(context, "add language en --Description:de=Englisch")
        );
        Assert.Contains("Method 'Description' does not exist on type", ex.Message);
    }

    [Fact]
    public void InvalidMethodArguments_ShouldThrowException()
    {
        var context = new TestContext();
        // Calling a method with invalid arguments should throw an exception.
        var ex = Assert.Throws<ArgumentException>(() =>
            RecordCommandRegistry.Run(context, "add language en --GetLabel:de=Englisch")
        );
        Assert.Contains("Method 'GetLabel' must have exactly 2 parameters", ex.Message);
    }

    [Fact]
    public void Generation_UsingDefaultOptions()
    {
        var options = new CommandGenerationOptions();
        var lang = new Language { Key = "en", Name = "English" };
        var cmd = RecordCommandRegistry<TestContext>.GenerateCommand(lang, options);
        Assert.Equal("add language en English", cmd);
    }

    [Fact]
    public void Generation_UsingPositionalProperties()
    {
        var options = new CommandGenerationOptions(usePositionalProperties: true);
        var lang = new Language { Key = "en", Name = "English" };
        var cmd = RecordCommandRegistry<TestContext>.GenerateCommand(lang, options);
        Assert.Equal("add language en English", cmd);
    }

    [Fact]
    public void Generation_UsingNamedArguments()
    {
        var options = new CommandGenerationOptions(usePositionalProperties: false);
        var lang = new Language { Key = "en", Name = "English" };
        var cmd = RecordCommandRegistry<TestContext>.GenerateCommand(lang, options);
        Assert.Equal("add language en --Name=English", cmd);
    }

    [Fact]
    public void Generation_UsingAliases()
    {
        var options = new CommandGenerationOptions(preferAliases: true);
        var lang = new Language { Key = "en", Name = "English" };
        var cmd = RecordCommandRegistry<TestContext>.GenerateCommand(lang, options);
        Assert.Equal("add lang en English", cmd);
    }

    [Fact]
    public void Generation_UsingSpaces()
    {
        var lang = new Language { Key = "en", Name = "English Language" };
        var cmd = RecordCommandRegistry<TestContext>.GenerateCommand(lang);
        Assert.Equal("add language en \"English Language\"", cmd);
    }

    [Fact]
    public void Generation_Books_Roundtrip()
    {
        var context = new TestContext();

        // Add books via commands (keep as regular string for newlines)
        const string dummyBooks = "add book 978-3-16-148410-0 \"The Book Title\" \"John Doe\" 2021\nadd book 978-3-16-148410-1 \"Another Book\" \"Jane Doe\" 2022\nadd book 978-3-16-148410-2 \"Third Book\" \"John Doe\" 2023";
        RecordCommandRegistry.RunMany(context, dummyBooks);

        // Generate commands for each book
        var commands = string.Join('\n', context.Books.Select(b => RecordCommandRegistry<TestContext>.GenerateCommand(b)));
        Assert.Equal(dummyBooks, commands);
    }

    [Fact]
    public void CustomCommands_SingleParameter()
    {
        var context = new TestContext();

        // Add a log entry via a custom command.
        RecordCommandRegistry.Run(context, "log \"This is a log entry\"");

        Assert.Single(context.Logs);
        Assert.Equal("This is a log entry", context.Logs.First());
    }

    [Fact]
    public void CustomCommands_AddLanguageToCountry()
    {
        var context = new TestContext();

        // Add a language record.
        RecordCommandRegistry.Run(context, "add language en English");

        // Add a country record.
        RecordCommandRegistry.Run(context, "add country be Belgium");

        // Add the language to the country.
        RecordCommandRegistry.Run(context, "add-language-to-country be en");

        Assert.Single(context.Countries);
        var country = context.Countries.First();
        Assert.Equal("be", country.Code, ignoreCase: true);
        Assert.Equal("Belgium", country.Name);
        Assert.NotNull(country.SpokenLanguages);
        Assert.Single(country.SpokenLanguages);
        Assert.Equal("en", country.SpokenLanguages.First());
    }

    [Fact]
    public void CustomCommands_WithOptionalParameters()
    {
        var context = new TestContext();

        // Add a language record.
        RecordCommandRegistry.Run(context, "update-language en English");

        Assert.Single(context.Languages);
        var lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
        Assert.Null(lang.GetLabel("en"));

        // Add with a label.
        RecordCommandRegistry.Run(context, "update-language en English Englisch");
        Assert.Single(context.Languages);
        lang = context.Languages.First();
        Assert.Equal("en", lang.Key, ignoreCase: true);
        Assert.Equal("English", lang.Name);
        Assert.Equal("Englisch", lang.GetLabel("en"));
    }

    [Fact]
    public void CustomCommands_Log2_WithOptionalParameters()
    {
        var context = new TestContext();

        // Add a log entry via a custom command with optional parameters.
        RecordCommandRegistry.Run(context, "log2 \"This is a log entry\"");
        RecordCommandRegistry.Run(context, "log2 \"This is another log entry\" false");
        RecordCommandRegistry.Run(context, "log2 \"This is a third log entry\" true 10");

        Assert.Equal(3, context.Logs.Count);
        Assert.Equal("This is a log entry;b=True;x=5", context.Logs[0]);
        Assert.Equal("This is another log entry;b=False;x=5", context.Logs[1]);
        Assert.Equal("This is a third log entry;b=True;x=10", context.Logs[2]);
    }

    [Fact]
    public void Arguments_Should_HandleEmptyString()
    {
        var context = new TestContext();

        // Add a log with an empty string.
        RecordCommandRegistry.Run(context, "log \"\"");

        Assert.Single(context.Logs);
        Assert.Equal("", context.Logs.First());
    }

    [Fact]
    public void Arguments_Should_HandleNullableValueTypes()
    {
        var context = new TestContext();

        // Call log3 with an integer value.
        RecordCommandRegistry.Run(context, "log3 \"This is a log entry\" 42");

        Assert.Single(context.Logs);
        Assert.Equal("This is a log entry;x=42", context.Logs.First());
    }

    [Fact]
    public void Arguments_VariousTypes()
    {
        var context = new TestContext();

        // Call log4 with various types of parameters.
        RecordCommandRegistry.RunMany(context, """
                                               log4 "This is a log entry" 2021-12-31 2021-12-31 3.14 12:34:56 12345678-1234-1234-1234-1234567890AB
                                               log4 "Another log entry" 2021-12-31 2021-12-31 3.14 12:34:56 12345678-1234-1234-1234-1234567890AB
                                               log4 "Third log entry" 2021-12-31 2021-12-31 3.14 12:34:56 12345678-1234-1234-1234-1234567890AB
                                               """);

        Assert.Equal(3, context.Logs.Count);
        Assert.Equal("This is a log entry;x=12/31/2021 00:00:00;y=12/31/2021;z=3.14,ts=12:34:56;g=12345678-1234-1234-1234-1234567890ab", context.Logs[0]);
        Assert.Equal("Another log entry;x=12/31/2021 00:00:00;y=12/31/2021;z=3.14,ts=12:34:56;g=12345678-1234-1234-1234-1234567890ab", context.Logs[1]);
        Assert.Equal("Third log entry;x=12/31/2021 00:00:00;y=12/31/2021;z=3.14,ts=12:34:56;g=12345678-1234-1234-1234-1234567890ab", context.Logs[2]);
    }

    [Fact]
    public void InvalidMethodArguments_CustomCommands_NotEnoughParameters()
    {
        var context = new TestContext();

        // Calling a custom method with not enough parameters should throw an exception.
        var ex = Assert.Throws<ArgumentException>(() =>
            RecordCommandRegistry.Run(context, "update-language en")
        );
        Assert.Contains("Method 'update-language' must have at least 2 parameter(s)", ex.Message);
    }

    [Fact]
    public void InvalidMethodArguments_CustomCommands_Log()
    {
        var context = new TestContext();

        // Calling a custom method with not enough parameters should throw an exception.
        var ex = Assert.Throws<ArgumentException>(() =>
            RecordCommandRegistry.Run(context, "log")
        );
        Assert.Contains("Method 'log' must have exactly 1 parameter", ex.Message);
    }

    [Fact]
    public void InvalidMethodArguments_CustomCommands_TooManyParameters()
    {
        var context = new TestContext();

        // Calling a custom method with too many parameters should throw an exception.
        var ex = Assert.Throws<ArgumentException>(() =>
            RecordCommandRegistry.Run(context, "update-language en English Englisch Extra")
        );
        Assert.Contains("Method 'update-language' must have at most 3 parameter(s)", ex.Message);
    }

    [Fact]
    public void CustomConversion_Unit()
    {
        var context = new TestContext();

        // Add a book with dimensions.
        RecordCommandRegistry.Run(context, "add book 978-3-16-148410-0 \"The Book Title\" \"John Doe\" --year=2021 --dimensions=\"2 m\"");

        var book = context.Books.First();
        Assert.NotNull(book.Dimensions);
        Assert.Equal(2, book.Dimensions.value);
        Assert.Equal("m", book.Dimensions.symbol);
    }

    [Fact]
    public void CustomConversion_Country()
    {
        var context = new TestContext();
        // Add a country record.
        RecordCommandRegistry.Run(context, "add country be Belgium");
        // Find the country by code.
        RecordCommandRegistry.Run(context, "add book 978-3-16-148410-0 \"The Book Title\" \"John Doe\" --year=2021 --origin-country=be");
        var book = context.Books.First();
        Assert.Equal("be", book.OriginCountry?.Code);
        Assert.Equal("Belgium", book.OriginCountry?.Name);
    }

    [Fact]
    public void Generation_GetUsageExample()
    {
        var country = RecordCommandRegistry<TestContext>.GetUsageExample<Country>();
        Assert.Equal("add country <Code> <Name> <SpokenLanguages>", country);

        var book = RecordCommandRegistry<TestContext>.GetUsageExample<Book>();
        //Assert.Equal("add book <ISBN> <Title> <Author> --year=<PublicationYear>", book);
        Assert.Equal("add book <ISBN> <Title> <Author> <PublicationYear>", book);
    }

    [Fact]
    public void Generation_GetUsageExample_WithAlias()
    {
        var country = RecordCommandRegistry<TestContext>.GetUsageExample<Country>(preferAliases: true);
        Assert.Equal("add ctr <Code> <Name> <langs>", country);

        var book = RecordCommandRegistry<TestContext>.GetUsageExample<Book>(preferAliases: true);
        //Assert.Equal("add bk <ISBN> <Title> <Author> --year=<year>", book);
        Assert.Equal("add bk <ISBN> <Title> <Author> <year>", book);
    }

    [Fact]
    public void Generation_GetDetailedUsageExample()
    {
        var country = RecordCommandRegistry<TestContext>.GetDetailedUsageExample<Country>();
        Assert.Equal("""
                     add country <Code> <Name> <SpokenLanguages>
                     # Parameter descriptions:
                     #   Code : string (quoted if contains spaces)
                     #   Name : string (quoted if contains spaces)
                     #   SpokenLanguages : array of string (quoted if contains spaces)
                     """, country);
    }

    [Fact]
    public void Generation_GetCustomCommandPrompt()
    {
        var log4 = RecordCommandRegistry<TestContext>.GetCustomCommandPrompt("log4");
        Assert.Equal("log4 <log> <x> <y> <z> <ts> <g>", log4);
    }

    [Fact]
    public void Generation_GetCustomCommandPrompt_DescribeParameters()
    {
        var log4 = RecordCommandRegistry<TestContext>.GetCustomCommandPrompt("log4", true);
        Assert.Equal("""
                     log4 <log> <x> <y> <z> <ts> <g>
                     #   log : string (quoted if contains spaces)
                     #   x : date (format yyyy-MM-dd)
                     #   y : date (format yyyy-MM-dd)
                     #   z : number
                     #   ts : timespan
                     #   g : guid

                     """, log4);
    }
}