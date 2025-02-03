namespace RecordCommander;

using System;

/// <summary>
/// Attribute to specify an alias for a command or property.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public class AliasAttribute(string name) : Attribute
{
    /// <summary>
    /// The name of the alias.
    /// </summary>
    public string Name { get; } = name;
}
