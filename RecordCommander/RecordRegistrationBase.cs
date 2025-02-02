using System.Collections;
using System.Reflection;

namespace RecordCommander;

/// <summary>
/// Base class for storing registration details for a record type.
/// </summary>
public abstract class RecordRegistrationBase
{
    /// <summary>
    /// The command name used to identify this record type.
    /// </summary>
    public string CommandName { get; }

    /// <summary>
    /// The type of the record.
    /// </summary>
    public Type RecordType { get; }

    /// <summary>
    /// The property that uniquely identifies a record.
    /// </summary>
    public PropertyInfo UniqueKeyProperty { get; }

    /// <summary>
    /// All public, settable properties (used to resolve named arguments).
    /// </summary>
    public Dictionary<string, PropertyInfo> AllProperties { get; }

    /// <summary>
    /// Positional properties (in order, after the unique key).
    /// </summary>
    public List<PropertyInfo> PositionalProperties { get; }

    protected RecordRegistrationBase(string commandName, Type recordType, PropertyInfo uniqueKeyProperty, List<PropertyInfo> positionalProperties)
    {
        CommandName = commandName;
        RecordType = recordType;
        UniqueKeyProperty = uniqueKeyProperty;
        PositionalProperties = positionalProperties;
        AllProperties = recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public abstract IList GetCollection(object context);
}