using System.Reflection;

namespace RecordCommander;

/// <summary>
/// Generic registration which ties together the context type and the record type.
/// </summary>
public class RecordRegistration<TContext, TRecord> : RecordRegistration<TContext>
{
    private readonly Func<TContext, string, TRecord?> _findRecord;
    private readonly Func<TContext, string, TRecord> _createRecord;

    public RecordRegistration(string name,
        Func<TContext, IList<TRecord>> collectionAccessor,
        PropertyInfo uniqueKeyProperty,
        List<PropertyInfo> positionalProperties)
        : base(name, typeof(TRecord), uniqueKeyProperty, positionalProperties)
    {
        _findRecord = (context, uniqueKey) =>
        {
            TRecord? record = default;

            // Retrieve the collection from the context.
            var collection = collectionAccessor(context);

            // Try to find an existing record (by comparing the unique key).
            foreach (var item in collection)
            {
                // TODO: Handle case where UniqueKey is not a string
                var keyVal = UniqueKeyProperty.GetValue(item) as string;
                if (string.Equals(keyVal, uniqueKey, StringComparison.OrdinalIgnoreCase))
                {
                    record = item;
                    break;
                }
            }

            return record;
        };
        _createRecord = (context, uniqueKey) =>
        {
            // Retrieve the collection from the context.
            var collection = collectionAccessor(context);

            // Create a new record if none exists.
            var record = Activator.CreateInstance<TRecord>() ?? throw new InvalidOperationException("Failed to create record.");
            UniqueKeyProperty.SetValue(record, uniqueKey);

            // Add the new record to the collection.
            collection.Add(record);

            return record;
        };
    }

    public RecordRegistration(string name,
        PropertyInfo uniqueKeyProperty,
        List<PropertyInfo> positionalProperties,
        Func<TContext, string, TRecord?> findRecord,
        Func<TContext, string, TRecord> createRecord)
        : base(name, typeof(TRecord), uniqueKeyProperty, positionalProperties)
    {
        _findRecord = findRecord;
        _createRecord = createRecord;
    }

    /// <inheritdoc />
    public override object FindOrCreateRecord(TContext context, string uniqueKey)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
#else
        if (context is null)
            throw new ArgumentNullException(nameof(context));
#endif

        return _findRecord(context, uniqueKey) ?? _createRecord(context, uniqueKey)!;
    }

    /// <inheritdoc />
    public override object? FindRecord(TContext context, string uniqueKey)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(context);
#else
        if (context is null)
            throw new ArgumentNullException(nameof(context));
#endif

        return _findRecord(context, uniqueKey);
    }
}
