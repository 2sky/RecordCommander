using System.ComponentModel;
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
            ? Helpers.GetAliasOrDefault(registration.Name, registration.RecordType)
            : registration.Name;

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
    public static string GetUsageExample<TRecord>(bool preferAliases = false, Func<PropertyInfo, bool>? filterProperty = null)
    {
        GetUsageExample(typeof(TRecord), preferAliases, filterProperty, out _, out var example);

        return example.ToString();
    }

    /// <summary>
    /// Generates a basic usage example (one line) based on registration.
    /// For example: add country &lt;code&gt; &lt;name&gt;
    /// </summary>
    public static string GetUsageExample(Type recordType, bool preferAliases = false, Func<PropertyInfo, bool>? filterProperty = null)
    {
        GetUsageExample(recordType, preferAliases, filterProperty, out _, out var example);

        return example.ToString();
    }

    private static void GetUsageExample(Type recordType, bool preferAliases, Func<PropertyInfo, bool>? filterProperty, out RecordRegistration<TContext> registration, out StringBuilder example)
    {
        registration = GetRegistration(recordType);

        example = new StringBuilder("add ");
        example.Append(preferAliases ? Helpers.GetAliasOrDefault(registration.Name, recordType) : registration.Name);
        example.Append(" <");
        example.Append(registration.UniqueKeyProperty.Name);
        example.Append('>');

        if (registration.PositionalProperties.Count > 0)
        {
            foreach (var positionalProperty in registration.PositionalProperties)
            {
                if (filterProperty != null && !filterProperty(positionalProperty))
                    continue;

                example.Append(" <");
                example.Append(preferAliases ? (Helpers.GetAlias(positionalProperty) ?? positionalProperty.Name) : positionalProperty.Name);
                example.Append('>');
            }
        }

        if (registration.NonPositionalProperties.Length > 0)
        {
            example.Append(" [");

            var first = true;
            foreach (var prop in registration.NonPositionalProperties)
            {
                if (filterProperty != null && !filterProperty(prop))
                    continue;

                if (first)
                    first = false;
                else
                    example.Append(' ');

                example.Append("--");
                var name = preferAliases ? (Helpers.GetAlias(prop) ?? prop.Name) : prop.Name;
                example.Append(name);
                example.Append("=<");
                example.Append(name);
                example.Append('>');
            }

            example.Append(']');
        }
    }

    /// <summary>
    /// Generates a detailed usage example including type descriptions as comments.
    /// </summary>
    public static string GetDetailedUsageExample<TRecord>(bool preferAliases = false, Func<PropertyInfo, bool>? filterProperty = null)
    {
        return GetDetailedUsageExample(typeof(TRecord), preferAliases, filterProperty);
    }

    /// <summary>
    /// Generates a detailed usage example including type descriptions as comments.
    /// </summary>
    public static string GetDetailedUsageExample(Type recordType, bool preferAliases = false, Func<PropertyInfo, bool>? filterProperty = null)
    {
        GetUsageExample(recordType, preferAliases, filterProperty, out var registration, out var sb);

        // Add type descriptions as comments.
        sb.AppendLine();
        sb.AppendLine("# Parameter descriptions:");

        void DescribeProperty(PropertyInfo property, string displayName)
        {
            sb.Append($"#   {displayName} : {Helpers.GetTypeDescription(property.PropertyType, _customConverterTypeDescriptions)}");

            var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrWhiteSpace(description))
            {
                sb.Append(" - ");
                sb.Append(description);
            }

            sb.AppendLine();
        }

        // Describe the unique key.
        var uniqueKey = registration.UniqueKeyProperty;
        DescribeProperty(uniqueKey, uniqueKey.Name);

        void DescribeProperties(IEnumerable<PropertyInfo> properties)
        {
            foreach (var prop in properties)
            {
                if (filterProperty != null && !filterProperty(prop))
                    continue;

                var displayName = preferAliases ? (Helpers.GetAlias(prop) ?? prop.Name) : prop.Name;
                DescribeProperty(prop, displayName);
            }
        }

        // Describe each positional property.
        DescribeProperties(registration.PositionalProperties);

        // Describe each non-positional property.
        DescribeProperties(registration.NonPositionalProperties);

        // (Optional) If you support method mappings, include them here.

        return sb.ToString().TrimEnd();
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
            {
                builder.Append($"#   {param.Name} : {Helpers.GetTypeDescription(param.ParameterType, _customConverterTypeDescriptions)}");

                // Check if we have a DescriptionAttribute
                var description = param.GetCustomAttribute<DescriptionAttribute>()?.Description;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    builder.Append(" - ");
                    builder.Append(description);
                }

                builder.AppendLine();
            }
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
    public static string GetTypeDescription(Type type, Dictionary<Type, string> customDescriptions)
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
            return $"array of {GetTypeDescription(type.GetElementType()!, customDescriptions)}";

        if (customDescriptions.TryGetValue(type, out var customDesc))
            return customDesc;

        return type.Name.ToLowerInvariant();
    }
}
