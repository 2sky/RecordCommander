using System.Reflection;

namespace RecordCommander;

/// <summary>
/// Base class for storing registration details for a record type.
/// </summary>
public abstract class RecordRegistration<TContext>
{
    /// <summary>
    /// The name used to identify this record type, will match record type name by default.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The CLR type of the record.
    /// </summary>
    public Type RecordType { get; }

    /// <summary>
    /// The property that uniquely identifies a record. Is always used as first argument.
    /// </summary>
    public PropertyInfo UniqueKeyProperty { get; }

    /// <summary>
    /// All public, settable properties (used to resolve named arguments).
    /// </summary>
    // TODO: Should we use read-only versions for this for safety?
    public Dictionary<string, PropertyInfo> AllProperties { get; }

    /// <summary>
    /// Positional properties (in order, after the unique key).
    /// </summary>
    // TODO: Should we use read-only versions for this for safety?
    public List<PropertyInfo> PositionalProperties { get; }

    /// <summary>
    /// Non-positional properties (used to resolve named arguments).
    /// </summary>
    public PropertyInfo[] NonPositionalProperties { get; }

    protected RecordRegistration(string name, Type recordType, PropertyInfo uniqueKeyProperty, List<PropertyInfo> positionalProperties)
    {
        Name = name;
        RecordType = recordType;
        UniqueKeyProperty = uniqueKeyProperty;
        PositionalProperties = positionalProperties;
        AllProperties = new(StringComparer.OrdinalIgnoreCase);

        var nonPositionalProperties = new List<PropertyInfo>();

        foreach (var property in recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                            .Where(p => p is { CanWrite: true, SetMethod.IsPublic: true }))
        {
            AllProperties[property.Name] = property;

            var aliasAttributes = property.GetCustomAttributes<AliasAttribute>();
            foreach (var alias in aliasAttributes)
                AllProperties[alias.Name] = property;

            if (property != uniqueKeyProperty && !positionalProperties.Contains(property))
                nonPositionalProperties.Add(property);
        }

        NonPositionalProperties = nonPositionalProperties.ToArray();
    }

    /// <summary>
    /// Find or create a record in the given context.
    /// </summary>
    public abstract object FindOrCreateRecord(TContext context, string uniqueKey);

    /// <summary>
    /// Add an alias for this record, e.g. "lng" for "language".
    /// </summary>
    public RecordRegistration<TContext> AddAlias(string alias) => RecordCommandRegistry<TContext>.AddAlias(Name, alias);
}
