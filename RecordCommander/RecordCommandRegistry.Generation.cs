using System.Reflection;
using System.Text;

namespace RecordCommander;

public static partial class RecordCommandRegistry<TContext>
{
    /// <summary>
    /// Given an entity instance, generates the command string to create or update it.
    /// Only properties with non-default values will be included.
    /// </summary>
    public static string GenerateCommand(object record, CommandGenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        options ??= CommandGenerationOptions.Default;

        var recordType = record.GetType(); // NOTE: Only used to find the registration, use the registration's RecordType instead.
        var registration = _registrations.Values.FirstOrDefault(r => r.RecordType == recordType)
            // Fallback to the first that is assignable from the entity type.
            ?? _registrations.Values.FirstOrDefault(r => r.RecordType.IsAssignableFrom(recordType))
            ?? throw new InvalidOperationException($"No registration found for type {recordType}.");

        // Determine command name (apply alias if requested)
        var cmdName = options.PreferAliases
            ? Helpers.GetAliasOrDefault(registration.CommandName, registration.RecordType)
            : registration.CommandName;

        var builder = new StringBuilder();
        builder.Append("add ");
        builder.Append(cmdName);
        builder.Append(' ');

        // Unique key: always include it.
        var uniqueKeyValue = registration.UniqueKeyProperty.GetValue(record);
        if (uniqueKeyValue is null)
            throw new InvalidOperationException("Unique key property cannot be null.");

        builder.Append(Helpers.ConvertValueToString(uniqueKeyValue, uniqueKeyValue.GetType()));

        // Prepare additional properties (all properties except the unique key)
        // that have non-default values.
        var additionalProps = new List<(PropertyInfo Prop, string Name, object? Value)>();
        foreach (var kvp in registration.AllProperties)
        {
            var prop = kvp.Value;
            // Skip the unique key property.
            if (prop.Name.Equals(registration.UniqueKeyProperty.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = prop.GetValue(record);
            if (Helpers.IsDefaultValue(value, prop))
                continue;

            // Use alias if requested.
            var propName = options.PreferAliases ? Helpers.GetAlias(prop) ?? prop.Name : prop.Name;
            additionalProps.Add((prop, propName, value));
        }

        // Append positional properties.
        if (options.UsePositionalProperties)
        {
            // For positional properties, use the order defined in PositionalProperties.
            foreach (var posProp in registration.PositionalProperties)
            {
                // Find a matching property in additionalProps (match by underlying property name)
                var match = additionalProps.FirstOrDefault(p => p.Prop.Name == posProp.Name);
                if (match.Prop != null)
                {
                    builder.Append(' ');
                    builder.Append(Helpers.ConvertValueToString(match.Value, posProp.PropertyType));
                    additionalProps.Remove(match);
                }
                else
                {
                    // Break since the remaining properties will not be in the correct order.
                    break;
                }
            }
        }

        // Use named arguments for all additional properties.
        foreach (var (prop, name, value) in additionalProps)
        {
            builder.Append(' ');
            builder.Append($"--{name}=");
            builder.Append(Helpers.ConvertValueToString(value, prop.PropertyType));
        }

        return builder.ToString().Trim();
    }
}

file static class Helpers
{
    /// <summary>
    /// Returns true if the value is considered "default" for the given type.
    /// For reference types, that is null.
    /// For value types, that is equal to default(T).
    /// </summary>
    public static bool IsDefaultValue(object? value, PropertyInfo property)
    {
        if (value == null)
            return true;

        var type = property.PropertyType;
        if (type.IsValueType)
        {
            var defaultValue = Activator.CreateInstance(Nullable.GetUnderlyingType(type) ?? type);
            return value.Equals(defaultValue);
        }

        return false;
    }

    /// <summary>
    /// Converts a value to a string suitable for a command.
    /// For arrays, produces a JSON-like array (using square brackets and comma-separated values).
    /// Otherwise, uses ToString().
    /// </summary>
    public static string ConvertValueToString(object? value, Type type)
    {
        if (value == null)
            return string.Empty;

        if (type.IsArray)
        {
            var array = (Array)value;
            var items = new List<string>();
            foreach (var item in array)
                items.Add(item is null ? string.Empty : ConvertValueToString(item, item.GetType()));

            return $"[{string.Join(',', items)}]";
        }

        // TODO: Handle other collection types (e.g. IList, IEnumerable)

        // For strings containing spaces, wrap them in quotes.
        var str = value.ToString() ?? string.Empty;
        // TODO: Handle escaping of quotes
        return str.Contains(' ') ? $"\"{str}\"" : str;
    }

    /// <summary>
    /// If the member (a property or type) has an AliasAttribute defined,
    /// returns the alias name. Otherwise, returns null.
    /// </summary>
    public static string? GetAlias(MemberInfo member) => member.GetCustomAttribute<AliasAttribute>()?.Name;

    /// <summary>
    /// For a given default name (such as CommandName) and type,
    /// returns the alias if available; otherwise, returns the default.
    /// </summary>
    public static string GetAliasOrDefault(string defaultName, Type type) => GetAlias(type) ?? defaultName;
}
