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
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(record);
#else
        if (record is null)
            throw new ArgumentNullException(nameof(record));
#endif

        options ??= CommandGenerationOptions.Default;

        var recordType = record.GetType(); // NOTE: Only used to find the registration, use the registration's RecordType instead.
        var registration = GetRegistration(recordType);

        // Determine command name (apply alias if requested)
        var cmdName = options.PreferAliases
            ? Helpers.GetAliasOrDefault(registration.CommandName, registration.RecordType)
            : registration.CommandName;

        var builder = new StringBuilder();
        builder.Append("add ");
        builder.Append(cmdName);
        builder.Append(' ');

        // Unique key: always include it.
        var uniqueKeyValue = registration.UniqueKeyProperty.GetValue(record) ?? throw new InvalidOperationException("Unique key property cannot be null.");

        builder.Append(Helpers.ConvertValueToString(uniqueKeyValue, uniqueKeyValue.GetType()));

        // Prepare additional properties (all properties except the unique key)
        // that have non-default values.
        var additionalProps = new HashSet<(PropertyInfo Prop, string Name, object? Value)>();
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

    private static RecordRegistration<TContext> GetRegistration(Type recordType)
    {
        return _registrations.Values.FirstOrDefault(r => r.RecordType == recordType)
               // Fallback to the first that is assignable from the entity type.
               ?? _registrations.Values.FirstOrDefault(r => r.RecordType.IsAssignableFrom(recordType))
               ?? throw new InvalidOperationException($"No registration found for type {recordType}.");
    }

    /// <summary>
    /// Generates a basic usage example (one line) based on registration.
    /// For example: add country &lt;code&gt; &lt;name&gt;
    /// </summary>
    public static string GetUsageExample<TRecord>(bool preferAliases = false)
    {
        var recordType = typeof(TRecord);
        var registration = GetRegistration(recordType);

        var cmdName = preferAliases ? Helpers.GetAliasOrDefault(registration.CommandName, recordType) : registration.CommandName;
        var uniqueKeyPlaceholder = $"<{registration.UniqueKeyProperty.Name}>";
        var positionalPlaceholders = registration.PositionalProperties
            .Select(prop => $"<{(preferAliases ? (Helpers.GetAlias(prop) ?? prop.Name) : prop.Name)}>");
        // TODO: Handle named arguments
        return $"add {cmdName} {uniqueKeyPlaceholder} {string.Join(" ", positionalPlaceholders)}".Trim();
    }

    /// <summary>
    /// Generates a detailed usage example including type descriptions as comments.
    /// </summary>
    public static string GetDetailedUsageExample<TRecord>(bool preferAliases = false)
    {
        var registration = GetRegistration(typeof(TRecord));

        // Generate the basic usage example:
        var usage = GetUsageExample<TRecord>(preferAliases);
        var sb = new StringBuilder();
        sb.AppendLine(usage);
        sb.AppendLine("# Parameter descriptions:");

        // Describe the unique key.
        var uniqueKeyName = registration.UniqueKeyProperty.Name;
        sb.AppendLine($"#   {uniqueKeyName} : {Helpers.GetTypeDescription(registration.UniqueKeyProperty.PropertyType)}");

        // Describe each positional property.
        foreach (var prop in registration.PositionalProperties)
        {
            var displayName = preferAliases ? (Helpers.GetAlias(prop) ?? prop.Name) : prop.Name;
            sb.AppendLine($"#   {displayName} : {Helpers.GetTypeDescription(prop.PropertyType)}");
        }

        // (Optional) If you support named parameters or method mappings, include them here.
        return sb.ToString().Trim();
    }

    public static string GetCustomCommandPrompt(string commandName, bool describeParameters = false)
    {
        if (!_extraCommands.TryGetValue(commandName, out var command))
            throw new ArgumentException($"Custom command '{commandName}' not found.", nameof(commandName));

        var builder = new StringBuilder();
        builder.Append(commandName);
        builder.Append(' ');

#if NET8_0_OR_GREATER
        var parameters = command.Parameters[1..];
#else
        var parameters = command.Parameters.Skip(1).ToArray();
#endif
        var requiredParams = command.RequiredParams - 1;

        for (var index = 0; index < parameters.Length; index++)
        {
            var param = parameters[index];

            if (index == requiredParams)
                builder.Append('[');

            builder.Append($"<{param.Name}>");

            if (index < parameters.Length - 1)
                builder.Append(' ');
            else if (requiredParams < parameters.Length)
                builder.Append(']');
        }

        if (describeParameters)
        {
            builder.AppendLine();

            // Describe the parameters by type
            foreach (var param in parameters)
                builder.AppendLine($"#   {param.Name} : {Helpers.GetTypeDescription(param.ParameterType)}");
        }

        return builder.ToString();
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

#if NET8_0_OR_GREATER
            return $"[{string.Join(',', items)}]";
#else
            return $"[{string.Join(",", items)}]";
#endif
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

    /// <summary>
    /// Returns a description string for a given type.
    /// </summary>
    public static string GetTypeDescription(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsEnum)
        {
            // TODO: Handle FlagsAttribute

            // List valid enum names.
            var names = string.Join("|", Enum.GetNames(type));
            return $"enum ({names})";
        }

        if (type == typeof(string))
            return "string (quoted if contains spaces)";

        // TODO: sbyte, byte, short, ushort, uint, ulong, char
        if (type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(float) || type == typeof(double))
            return "number";

        if (type == typeof(DateTime)
#if NET8_0_OR_GREATER
            || type == typeof(DateOnly)
#endif
            )
            return "date (format yyyy-MM-dd)";

        if (type == typeof(bool))
            return "boolean (true/false)";

        if (type.IsArray)
            return $"array of {GetTypeDescription(type.GetElementType()!)}";

        return type.Name.ToLowerInvariant();
    }
}
