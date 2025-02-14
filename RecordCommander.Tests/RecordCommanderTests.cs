namespace RecordCommander.Tests;

// Sample domain classes for testing
public class TestContext
{
    private readonly List<Book> books = [];

    public List<Language> Languages { get; set; } = [];
    public List<Country> Countries { get; set; } = [];

    public ICollection<Book> Books => books;

    public Book? FindBook(string isbn) => books.FirstOrDefault(b => b.ISBN == isbn);

    public Book CreateBook(string isbn)
    {
        var book = new Book { ISBN = isbn };
        books.Add(book);
        return book;
    }
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
}