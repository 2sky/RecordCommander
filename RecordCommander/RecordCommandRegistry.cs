using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace RecordCommander;

/// <summary>
/// The registry that holds all registered record types and parses commands.
/// </summary>
public static class RecordCommandRegistry
{
    private static readonly Dictionary<string, RecordRegistrationBase> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a record type with its configuration.
    /// </summary>
    /// <typeparam name="TContext">Type of the context (for example, your data container).</typeparam>
    /// <typeparam name="TRecord">Record type (for example, Language or Country).</typeparam>
    /// <param name="commandName">The command token to use (e.g. "language").</param>
    /// <param name="collectionAccessor">A lambda to extract the collection (e.g. ctx => ctx.Languages).</param>
    /// <param name="uniqueKeySelector">An expression to select the unique key property (e.g. x => x.Key).</param>
    /// <param name="positionalPropertySelectors">Expressions for additional (positional) properties.</param>
    public static void Register<TContext, TRecord>(
        string commandName,
        Func<TContext, IList<TRecord>> collectionAccessor,
        Expression<Func<TRecord, string>> uniqueKeySelector,
        params Expression<Func<TRecord, object>>[] positionalPropertySelectors)
        where TRecord : new()
    {
        // Get the unique key property info from the expression.
        var uniqueKeyProp = GetPropertyInfo(uniqueKeySelector);
        // Get positional property infos.
        var positionalProps = positionalPropertySelectors.Select(GetPropertyInfo).ToList();
        var registration = new RecordRegistration<TContext, TRecord>(commandName, collectionAccessor, uniqueKeyProp, positionalProps);
        _registrations[commandName] = registration;
    }

    // Helper to extract a PropertyInfo from an expression.
    private static PropertyInfo GetPropertyInfo<T, TProp>(Expression<Func<T, TProp>> expr)
    {
        return expr.Body switch
        {
            MemberExpression memberExpr => (PropertyInfo)memberExpr.Member,
            UnaryExpression { Operand: MemberExpression memberExpr2 } => (PropertyInfo)memberExpr2.Member,
            _ => throw new ArgumentException("Invalid expression", nameof(expr)),
        };
    }

    /// <summary>
    /// Parses and runs a command string (e.g. "add language nl Dutch").
    /// </summary>
    /// <typeparam name="TContext">The type of the context (e.g. MyData).</typeparam>
    /// <param name="context">The context instance.</param>
    /// <param name="command">The command string.</param>
    public static void Run<TContext>(TContext context, string command)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tokens = Tokenize(command);
        if (tokens.Count == 0)
            throw new ArgumentException("Command is empty", nameof(command));

        // For now, we only support the "add" action.
        var action = tokens[0].ToLowerInvariant();
        if (action != "add")
            throw new ArgumentException($"Action '{action}' not supported", nameof(command));

        if (tokens.Count < 3)
            throw new ArgumentException("Command must have at least 3 tokens: 'add', type, key", nameof(command));

        var typeName = tokens[1];
        // TODO: Handle aliases for types.
        if (!_registrations.TryGetValue(typeName, out var registration))
            throw new ArgumentException($"Type '{typeName}' is not registered", nameof(command));

        var uniqueKeyValueStr = tokens[2];

        // Process the rest of the tokens.
        var positionalArgs = new List<string>();
        var namedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 3; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.StartsWith("--"))
            {
                // Expect format --PropertyName=value
                var eqIndex = token.IndexOf('=');
                if (eqIndex < 0)
                {
                    throw new ArgumentException($"Named argument '{token}' must be in the format --Property=value");
                }
                var propName = token.Substring(2, eqIndex - 2);
                var propValue = token[(eqIndex + 1)..];
                namedArgs[propName] = propValue;
            }
            else
                positionalArgs.Add(token);
        }

        // TODO: Collection could also be an IQueryable, so we could use LINQ to query it.

        // Retrieve the collection from the context.
        var collection = registration.GetCollection(context);

        // Try to find an existing record (by comparing the unique key).
        object? record = null;
        foreach (var item in collection)
        {
            var keyVal = registration.UniqueKeyProperty.GetValue(item) as string;
            if (string.Equals(keyVal, uniqueKeyValueStr, StringComparison.OrdinalIgnoreCase))
            {
                record = item;
                break;
            }
        }
        if (record is null)
        {
            // Create a new record if none exists.
            record = Activator.CreateInstance(registration.RecordType);
            registration.UniqueKeyProperty.SetValue(record, uniqueKeyValueStr);
            collection.Add(record);
        }

        // Update the record with positional arguments.
        for (var i = 0; i < registration.PositionalProperties.Count && i < positionalArgs.Count; i++)
        {
            var prop = registration.PositionalProperties[i];
            var argValue = positionalArgs[i];
            var converted = ConvertToType(argValue, prop.PropertyType);
            prop.SetValue(record, converted);
        }

        // Update with named arguments.
        foreach (var kvp in namedArgs)
        {
            // TODO: Handle aliases for property names.
            if (!registration.AllProperties.TryGetValue(kvp.Key, out var prop))
            {
                throw new ArgumentException($"Property '{kvp.Key}' does not exist on type '{registration.RecordType.Name}'");
            }
            var converted = ConvertToType(kvp.Value, prop.PropertyType);
            prop.SetValue(record, converted);
        }
    }

    /// <summary>
    /// Converts a string value into a value of the specified type.
    /// Special handling is included for array types (expecting a JSON–like format).
    /// </summary>
    private static object ConvertToType(string value, Type targetType)
    {
        // TODO: Handle custom conversions (e.g. enums, DateTime, etc.)

        if (targetType == typeof(string))
            return value;

        if (targetType.IsArray)
        {
            // Expect a JSON array syntax.
            var json = value;
            if (json.StartsWith('[') && json.EndsWith(']'))
            {
                // If the array does not contain any quotes, assume it's a comma-separated list without quotes.
                if (!json.Contains('"') && !json.Contains('\''))
                {
                    // Remove the brackets and split by comma.
                    var inner = json.Substring(1, json.Length - 2).Trim();
                    if (string.IsNullOrEmpty(inner))
                    {
                        json = "[]";
                    }
                    else
                    {
                        // Split on commas and add quotes around each element.
                        var items = inner
                            .Split(',')
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Select(s => $"\"{s}\"");
                        json = "[" + string.Join(",", items) + "]";
                    }
                }
                else if (json.Contains('\'') && !json.Contains('"'))
                {
                    // Replace single quotes with double quotes if only single quotes are present.
                    json = json.Replace('\'', '"');
                }
                var elementType = targetType.GetElementType();
                var listType = typeof(List<>).MakeGenericType(elementType);
                try
                {
                    var deserialized = System.Text.Json.JsonSerializer.Deserialize(json, listType);
                    var toArrayMethod = listType.GetMethod("ToArray");
                    return toArrayMethod.Invoke(deserialized, null);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Failed to parse array value: {value}", ex);
                }
            }
            else
            {
                throw new ArgumentException($"Value '{value}' is not a valid array representation");
            }
        }

        // For primitives (int, bool, etc.) use ChangeType.
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to convert value '{value}' to type {targetType.Name}", ex);
        }
    }


    /// <summary>
    /// A simple tokenizer that splits a command string into tokens.
    /// Supports quotes (single or double) and simple escape sequences.
    /// </summary>
    private static List<string> Tokenize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var quoteChar = '\0';

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (inQuotes)
            {
                if (c == quoteChar)
                {
                    inQuotes = false;
                }
                else if (c == '\\' && i + 1 < input.Length)
                {
                    i++;
                    current.Append(input[i]);
                }
                else
                    current.Append(c);
            }
            else
            {
                if (c is '"' or '\'')
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                    current.Append(c);
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }
}