﻿using System.Reflection;

namespace RecordCommander;

/// <summary>
/// Base class for storing registration details for a record type.
/// </summary>
public abstract class RecordRegistration<TContext>
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
    // TODO: Should we use read-only versions for this for safety?
    public Dictionary<string, PropertyInfo> AllProperties { get; }

    /// <summary>
    /// Positional properties (in order, after the unique key).
    /// </summary>
    // TODO: Should we use read-only versions for this for safety?
    public List<PropertyInfo> PositionalProperties { get; }

    protected RecordRegistration(string commandName, Type recordType, PropertyInfo uniqueKeyProperty, List<PropertyInfo> positionalProperties)
    {
        CommandName = commandName;
        RecordType = recordType;
        UniqueKeyProperty = uniqueKeyProperty;
        PositionalProperties = positionalProperties;
        AllProperties = new(StringComparer.OrdinalIgnoreCase);

        foreach (var property in recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            AllProperties[property.Name] = property;

            var aliasAttributes = property.GetCustomAttributes<AliasAttribute>();
            foreach (var alias in aliasAttributes)
                AllProperties[alias.Name] = property;
        }
    }

    /// <summary>
    /// Find or create a record in the given context.
    /// </summary>
    public abstract object FindOrCreateRecord(TContext context, string uniqueKey);

    /// <summary>
    /// Add an alias for this command.
    /// </summary>
    public RecordRegistration<TContext> AddAlias(string alias) => RecordCommandRegistry<TContext>.AddAlias(CommandName, alias);
}
