namespace RecordCommander.Tests;

// Sample domain classes for testing
public class TestContext
{
    public List<Language> Languages { get; set; } = [];
    public List<Country> Countries { get; set; } = [];
}

[Alias("lang")]
public class Language
{
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class Country
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    [Alias("langs")]
    public string[] SpokenLanguages { get; set; } = [];
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
}