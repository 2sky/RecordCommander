namespace RecordCommander;

/// <summary>
/// Options to control how commands are generated from an entity.
/// </summary>
public sealed class CommandGenerationOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandGenerationOptions"/> class.
    /// </summary>
    /// <param name="preferAliases">If true, use any defined aliases for the command and property names.</param>
    /// <param name="usePositionalProperties">If true, output property values as positional parameters.</param>
    /// <param name="ignoreDefaultValues">If true, ignore properties with default values.</param>
    public CommandGenerationOptions(bool preferAliases = false, bool usePositionalProperties = true, bool ignoreDefaultValues = true)
    {
        PreferAliases = preferAliases;
        UsePositionalProperties = usePositionalProperties;
        IgnoreDefaultValues = ignoreDefaultValues;
    }

    /// <summary>
    /// Gets or sets the default options to use.
    /// </summary>
    public static CommandGenerationOptions Default { get; set; } = new();

    /// <summary>
    /// If true, use any defined aliases for the command and property names.
    /// Otherwise, use the full names.
    /// </summary>
    public bool PreferAliases { get; }

    /// <summary>
    /// If true, output property values as positional parameters (in the order defined by the registration).
    /// Otherwise, use named arguments.
    /// </summary>
    public bool UsePositionalProperties { get; }

    /// <summary>
    /// If true, ignore properties with default values.
    /// </summary>
    public bool IgnoreDefaultValues { get; }
}
