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
    /// Registers a record type with its configuration.
    /// </summary>
    /// <typeparam name="TContext">Type of the context (for example, your data container).</typeparam>
    /// <typeparam name="TRecord">Record type (for example, Language or Country).</typeparam>
    /// <param name="commandName">The command token to use (e.g. "language"), this is case-insensitive.</param>
    /// <param name="findRecord">A lambda to find an existing record (e.g. (ctx, key) => ctx.Languages.FirstOrDefault(x => x.Key == key)).</param>
    /// <param name="createRecord">A lambda to create a new record and track it on the context (e.g. (ctx, key) => { var lang = new Language { Key = key }; ctx.Languages.Add(lang); return lang; }).</param>
    /// <param name="uniqueKeySelector">An expression to select the unique key property (e.g. x => x.Key).</param>
    /// <param name="positionalPropertySelectors">Expressions for additional (positional) properties.</param>
    public static RecordRegistration<TContext, TRecord> Register<TContext, TRecord>(
        string commandName,
        Func<TContext, string, TRecord?> findRecord,
        Func<TContext, string, TRecord> createRecord,
        Expression<Func<TRecord, string>> uniqueKeySelector,
        params Expression<Func<TRecord, object>>[] positionalPropertySelectors)
    {
        return RecordCommandRegistry<TContext>.Register(commandName, findRecord, createRecord, uniqueKeySelector, positionalPropertySelectors);
    }

    /// <summary>
    /// Registers a record type with its configuration.
    /// </summary>
    /// <typeparam name="TContext">Type of the context (for example, your data container).</typeparam>
    /// <typeparam name="TRecord">Record type (for example, Language or Country).</typeparam>
    /// <param name="commandName">The command token to use (e.g. "language"), this is case-insensitive.</param>
    /// <param name="findOrCreateRecord">A lambda to find an existing record or create a new record (e.g. (ctx, key) => ctx.GetOrCreateLanguage(key)).</param>
    /// <param name="uniqueKeySelector">An expression to select the unique key property (e.g. x => x.Key).</param>
    /// <param name="positionalPropertySelectors">Expressions for additional (positional) properties.</param>
    public static RecordRegistration<TContext, TRecord> Register<TContext, TRecord>(
        string commandName,
        Func<TContext, string, TRecord> findOrCreateRecord,
        Expression<Func<TRecord, string>> uniqueKeySelector,
        params Expression<Func<TRecord, object>>[] positionalPropertySelectors)
    {
        return RecordCommandRegistry<TContext>.Register(commandName, findOrCreateRecord, uniqueKeySelector, positionalPropertySelectors);
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
public static partial class RecordCommandRegistry<TContext>
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
        return Register(commandName, collectionAccessor, null, null, uniqueKeySelector, positionalPropertySelectors);
    }

    /// <summary>
    /// Registers a record type with its configuration.
    /// </summary>
    /// <typeparam name="TRecord">Record type (for example, Language or Country).</typeparam>
    /// <param name="commandName">The command token to use (e.g. "language"), this is case-insensitive.</param>
    /// <param name="findRecord">A lambda to find an existing record (e.g. (ctx, key) => ctx.Languages.FirstOrDefault(x => x.Key == key)).</param>
    /// <param name="createRecord">A lambda to create a new record and track it on the context (e.g. (ctx, key) => { var lang = new Language { Key = key }; ctx.Languages.Add(lang); return lang; }).</param>
    /// <param name="uniqueKeySelector">An expression to select the unique key property (e.g. x => x.Key).</param>
    /// <param name="positionalPropertySelectors">Expressions for additional (positional) properties.</param>
    public static RecordRegistration<TContext, TRecord> Register<TRecord>(
        string commandName,
        Func<TContext, string, TRecord?> findRecord,
        Func<TContext, string, TRecord> createRecord,
        Expression<Func<TRecord, string>> uniqueKeySelector,
        params Expression<Func<TRecord, object>>[] positionalPropertySelectors)
    {
        return Register(commandName, null, findRecord, createRecord, uniqueKeySelector, positionalPropertySelectors);
    }

    /// <summary>
    /// Registers a record type with its configuration.
    /// </summary>
    /// <typeparam name="TRecord">Record type (for example, Language or Country).</typeparam>
    /// <param name="commandName">The command token to use (e.g. "language"), this is case-insensitive.</param>
    /// <param name="findOrCreateRecord">A lambda to find an existing record or create a new record (e.g. (ctx, key) => ctx.GetOrCreateLanguage(key)).</param>
    /// <param name="uniqueKeySelector">An expression to select the unique key property (e.g. x => x.Key).</param>
    /// <param name="positionalPropertySelectors">Expressions for additional (positional) properties.</param>
    public static RecordRegistration<TContext, TRecord> Register<TRecord>(
        string commandName,
        Func<TContext, string, TRecord> findOrCreateRecord,
        Expression<Func<TRecord, string>> uniqueKeySelector,
        params Expression<Func<TRecord, object>>[] positionalPropertySelectors)
    {
        return Register(commandName, null, findOrCreateRecord, findOrCreateRecord, uniqueKeySelector, positionalPropertySelectors);
    }

    private static RecordRegistration<TContext, TRecord> Register<TRecord>(
        string commandName,
        Func<TContext, IList<TRecord>>? collectionAccessor,
        Func<TContext, string, TRecord?>? findRecord,
        Func<TContext, string, TRecord>? createRecord,
        Expression<Func<TRecord, string>> uniqueKeySelector,
        params Expression<Func<TRecord, object>>[] positionalPropertySelectors)
    {
        // Get the unique key property info from the expression.
        var uniqueKeyProp = Helpers.GetPropertyInfo(uniqueKeySelector);
        // Get positional property infos.
        var positionalProps = positionalPropertySelectors.Select(Helpers.GetPropertyInfo).ToList();

        RecordRegistration<TContext, TRecord> registration;
        if (collectionAccessor is not null)
            registration = new(commandName, collectionAccessor, uniqueKeyProp, positionalProps);
        else if (findRecord is not null && createRecord is not null)
            registration = new(commandName, uniqueKeyProp, positionalProps, findRecord, createRecord);
        else
            throw new ArgumentException("Either collectionAccessor or findRecord and createRecord must be provided");

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

    /// <summary>
    /// Parses and runs a command string (e.g. "add language nl Dutch").
    /// </summary>
    /// <param name="context">The context instance.</param>
    /// <param name="command">The command string.</param>
    public static void Run(TContext context, string command)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
#else
        if (context is null)
            throw new ArgumentNullException(nameof(context));
#endif

        var tokens = Helpers.Tokenize(command);
        if (tokens.Count == 0)
            throw new ArgumentException("Command is empty", nameof(command));

        // For now, we only support the "add" action.
        var action = tokens[0].ToLowerInvariant();
        if (action != "add")
            throw new NotSupportedException($"Action '{action}' not supported");

        if (tokens.Count < 3)
            throw new NotSupportedException("Command must have at least 3 tokens: 'add', type, key");

        var typeName = tokens[1];
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
#if NET8_0_OR_GREATER
                var propValue = token[(eqIndex + 1)..];
#else
                var propValue = token.Substring(eqIndex + 1);
#endif
                namedArgs[propName] = propValue;
            }
            else
                positionalArgs.Add(token);
        }

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
#if NET8_0_OR_GREATER
        foreach (var (key, value) in namedArgs)
        {
#else
        foreach (var kvp in namedArgs)
        {
            var key = kvp.Key;
            var value = kvp.Value;
#endif
            if (!registration.AllProperties.TryGetValue(key, out var prop))
            {
                // Check if the key contains a colon as this indicates we're dealing with a method call.
                // --Label:en=English may be a method call as SetLabel("en", "English") or Label("en", "English").
                var colonIndex = key.IndexOf(':');
                if (colonIndex > 0)
                {
#if NET8_0_OR_GREATER
                    var methodName = key[..colonIndex];
                    var args = key[(colonIndex + 1)..];
#else
                    var methodName = key.Substring(0, colonIndex);
                    var args = key.Substring(colonIndex + 1);
#endif
                    var method = registration.RecordType.GetMethod(methodName) ?? registration.RecordType.GetMethod("Set" + methodName);
                    if (method is null)
                        throw new ArgumentException($"Method '{methodName}' does not exist on type '{registration.RecordType.Name}'");

                    var parameters = method.GetParameters();
                    if (parameters.Length != 2)
                        throw new ArgumentException($"Method '{methodName}' must have exactly 2 parameters, got {parameters.Length} instead");

                    method.Invoke(record, [ConvertToType(args, parameters[0].ParameterType), ConvertToType(value, parameters[1].ParameterType)]);
                    continue;
                }

                throw new ArgumentException($"Property '{key}' does not exist on type '{registration.RecordType.Name}'");
            }

            var converted = ConvertToType(value, prop.PropertyType);
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
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
#else
        if (context is null)
            throw new ArgumentNullException(nameof(context));
#endif

        var lines = commands.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var sanitized = line.Trim();
#if NET8_0_OR_GREATER
            if (string.IsNullOrEmpty(sanitized) || sanitized.StartsWith('#'))
#else
            if (string.IsNullOrEmpty(sanitized) || sanitized.StartsWith("#", StringComparison.Ordinal))
#endif
                continue;

            Run(context, sanitized);
        }
    }

    /// <summary>
    /// Converts a string value into a value of the specified type.
    /// Special handling is included for array types (expecting a JSON–like format).
    /// </summary>
    private static object? ConvertToType(string value, Type targetType)
    {
        // TODO: Handle custom conversions (e.g. enums, DateTime, etc.)
        // TODO: Handle nullable types

        if (targetType == typeof(string))
            return value;

        if (targetType.IsArray)
        {
            // Expect a JSON array syntax.
            var json = value;
#if NET8_0_OR_GREATER
            if (json.StartsWith('[') && json.EndsWith(']'))
#else
            if (json.StartsWith("[", StringComparison.Ordinal) && json.EndsWith("]", StringComparison.Ordinal))
#endif
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

        if (targetType.IsEnum)
        {
#if NET8_0_OR_GREATER
            if (Enum.TryParse(targetType, value, true, out var result))
                return result;
#else
            try
            {
                return Enum.Parse(targetType, value, true);
            }
            catch
            {
                // NOTE: We'll throw an exception below without more information
            }
#endif

            throw new ArgumentException($"Failed to parse enum value '{value}' for type {targetType.Name}");
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
}

file static class Helpers
{
    /// <summary>
    /// Extracts the PropertyInfo from an expression.
    /// </summary>
    public static PropertyInfo GetPropertyInfo<T, TProp>(Expression<Func<T, TProp>> expr)
    {
        return expr.Body switch
        {
            MemberExpression memberExpr => (PropertyInfo)memberExpr.Member,
            UnaryExpression { Operand: MemberExpression memberExpr2 } => (PropertyInfo)memberExpr2.Member,
            _ => throw new ArgumentException("Invalid expression", nameof(expr)),
        };
    }

    /// <summary>
    /// A simple tokenizer that splits a command string into tokens.
    /// Supports quotes (single or double) and simple escape sequences.
    /// </summary>
    public static List<string> Tokenize(string input)
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
