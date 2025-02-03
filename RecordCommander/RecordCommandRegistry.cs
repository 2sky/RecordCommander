using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace RecordCommander;

/// <summary>
/// The registry that holds all registered record types and parses commands.
/// </summary>
public static class RecordCommandRegistry
{
    /// <summary>
    /// Registers a record type with its configuration.
    /// </summary>
    /// <typeparam name="TContext">Type of the context (for example, your data container).</typeparam>
    /// <typeparam name="TRecord">Record type (for example, Language or Country).</typeparam>
    /// <param name="commandName">The command token to use (e.g. "language"), this is case-insensitive.</param>
    /// <param name="collectionAccessor">A lambda to extract the collection (e.g. ctx => ctx.Languages).</param>
    /// <param name="uniqueKeySelector">An expression to select the unique key property (e.g. x => x.Key).</param>
    /// <param name="positionalPropertySelectors">Expressions for additional (positional) properties.</param>
    public static RecordRegistration<TContext, TRecord> Register<TContext, TRecord>(
        string commandName,
        Func<TContext, IList<TRecord>> collectionAccessor,
        Expression<Func<TRecord, string>> uniqueKeySelector,
        params Expression<Func<TRecord, object>>[] positionalPropertySelectors)
        where TRecord : new()
    {
        return RecordCommandRegistry<TContext>.Register(commandName, collectionAccessor, uniqueKeySelector, positionalPropertySelectors);
    }

    /// <summary>
    /// Parses and runs a command string (e.g. "add language nl Dutch").
    /// </summary>
    /// <typeparam name="TContext">Type of the context (for example, your data container).</typeparam>
    /// <param name="context">The context instance.</param>
    /// <param name="command">The command string.</param>
    public static void Run<TContext>(TContext context, string command) => RecordCommandRegistry<TContext>.Run(context, command);

    /// <summary>
    /// Parses and runs multiple commands from a string.
    /// </summary>
    /// <typeparam name="TContext">Type of the context (for example, your data container).</typeparam>
    /// <param name="context">The context instance.</param>
    /// <param name="commands">The commands string (separated by newlines).</param>
    public static void RunMany<TContext>(TContext context, string commands) => RecordCommandRegistry<TContext>.RunMany(context, commands);
}

/// <summary>
/// The registry that holds all registered record types and parses commands.
/// </summary>
public static class RecordCommandRegistry<TContext>
{
    // TODO: Should we have an option to freeze/lock the registry to prevent further registrations?

    private static readonly Dictionary<string, RecordRegistration<TContext>> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if a command is registered.
    /// </summary>
    public static bool IsRegistered(string commandName) => _registrations.ContainsKey(commandName);

    /// <summary>
    /// Registers a record type with its configuration.
    /// </summary>
    /// <typeparam name="TRecord">Record type (for example, Language or Country).</typeparam>
    /// <param name="commandName">The command token to use (e.g. "language"), this is case-insensitive.</param>
    /// <param name="collectionAccessor">A lambda to extract the collection (e.g. ctx => ctx.Languages).</param>
    /// <param name="uniqueKeySelector">An expression to select the unique key property (e.g. x => x.Key).</param>
    /// <param name="positionalPropertySelectors">Expressions for additional (positional) properties.</param>
    public static RecordRegistration<TContext, TRecord> Register<TRecord>(
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

        var aliasAttributes = typeof(TRecord).GetCustomAttributes<AliasAttribute>();
        foreach (var alias in aliasAttributes)
            _registrations[alias.Name] = registration;

        return registration;
    }

    /// <summary>
    /// Adds an alias for a command.
    /// </summary>
    public static RecordRegistration<TContext> AddAlias(string commandName, string alias)
    {
        if (!_registrations.TryGetValue(commandName, out var registration))
            throw new ArgumentException($"Command '{commandName}' is not registered", nameof(commandName));

        _registrations[alias] = registration;

        return registration;
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
    /// <param name="context">The context instance.</param>
    /// <param name="command">The command string.</param>
    public static void Run(TContext context, string command)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tokens = Tokenize(command);
        if (tokens.Count == 0)
            throw new ArgumentException("Command is empty", nameof(command));

        // For now, we only support the "add" action.
        var action = tokens[0].ToLowerInvariant();
        if (action != "add")
            throw new NotSupportedException($"Action '{action}' not supported");

        if (tokens.Count < 3)
            throw new NotSupportedException("Command must have at least 3 tokens: 'add', type, key");

        var typeName = tokens[1];
        // TODO: Handle aliases for types.
        if (!_registrations.TryGetValue(typeName, out var registration))
            throw new NotSupportedException($"Type '{typeName}' is not registered");

        var uniqueKey = tokens[2];

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

        var record = registration.FindOrCreateRecord(context, uniqueKey);

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
            if (!registration.AllProperties.TryGetValue(kvp.Key, out var prop))
                throw new ArgumentException($"Property '{kvp.Key}' does not exist on type '{registration.RecordType.Name}'");

            var converted = ConvertToType(kvp.Value, prop.PropertyType);
            prop.SetValue(record, converted);
        }
    }

    /// <summary>
    /// Parses and runs multiple commands from a string.
    /// </summary>
    /// <param name="context">The context instance.</param>
    /// <param name="commands">The commands string (separated by newlines).</param>
    public static void RunMany(TContext context, string commands)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lines = commands.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
            Run(context, line);
    }

    /// <summary>
    /// Converts a string value into a value of the specified type.
    /// Special handling is included for array types (expecting a JSON–like format).
    /// </summary>
    private static object ConvertToType(string value, Type targetType)
    {
        // TODO: Handle custom conversions (e.g. enums, DateTime, etc.)
        // TODO: Handle nullable types

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
                if (elementType is null)
                    throw new ArgumentException("Invalid array type", nameof(targetType));

                var listType = typeof(List<>).MakeGenericType(elementType);
                try
                {
                    var deserialized = System.Text.Json.JsonSerializer.Deserialize(json, listType);
                    var toArrayMethod = listType.GetMethod("ToArray");
                    if (toArrayMethod is null)
                        throw new ArgumentException("Failed to find ToArray method", nameof(targetType));

                    return toArrayMethod.Invoke(deserialized, null);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Failed to parse array value: {value}", ex);
                }
            }

            throw new ArgumentException($"Value '{value}' is not a valid array representation, expected JSON array syntax");
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
