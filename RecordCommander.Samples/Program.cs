namespace RecordCommander.Samples
{
    // Define your context and domain classes.
    public class MyData
    {
        public List<Language> Languages { get; set; } = [];
        public List<Country> Countries { get; set; } = [];
    }

    [Alias("lang")]
    public class Language
    {
        private readonly Dictionary<string, string> labels = [];

        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;

        public void SetLabel(string culture, string label) => labels[culture] = label;

        public string? GetLabel(string culture) => labels.GetValueOrDefault(culture);

        public override string ToString() => RecordCommandRegistry<MyData>.GenerateCommand(this); // TODO: Doesn't handle Labels yet
    }

    public class Country
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string[] SpokenLanguages { get; set; } = [];

        public override string ToString() => RecordCommandRegistry<MyData>.GenerateCommand(this);
    }

    internal class Program
    {
        private static void Main()
        {
            // Register the record types.
            RecordCommandRegistry.Register<MyData, Language>(
                commandName: "language",
                collectionAccessor: ctx => ctx.Languages,
                uniqueKeySelector: x => x.Key,
                positionalPropertySelectors: [x => x.Name]
            );

            RecordCommandRegistry.Register<MyData, Country>(
                commandName: "country",
                collectionAccessor: ctx => ctx.Countries,
                uniqueKeySelector: x => x.Code,
                positionalPropertySelectors: [x => x.Name]
            );

            // Create a context instance.
            var context = new MyData();

            // Run a few commands.
            RecordCommandRegistry.Run(context, "add lang nl Dutch --Label:nl=Nederlands");
            RecordCommandRegistry.Run(context, "add lang nl --Label:de=Niederländisch --Label:fr=Néerlandais");
            RecordCommandRegistry.Run(context, "add language fr French");
            RecordCommandRegistry.Run(context, "add country be Belgium");
            // In this command, we update the already–created country "be" by providing the SpokenLanguages via a named argument.
            RecordCommandRegistry.Run(context, "add country be --SpokenLanguages=['nl','fr']");

            // Show the result.
            Console.WriteLine("Languages:");
            foreach (var lang in context.Languages)
            {
                Console.WriteLine($"Key: {lang.Key}, Name: {lang.Name}");
            }
            Console.WriteLine("\nCountries:");
            foreach (var country in context.Countries)
            {
                Console.WriteLine($"Code: {country.Code}, Name: {country.Name}, SpokenLanguages: {string.Join(", ", country.SpokenLanguages)}");
            }
        }
    }
}
