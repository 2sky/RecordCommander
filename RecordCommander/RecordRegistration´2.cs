using System.Reflection;

namespace RecordCommander;

/// <summary>
/// Generic registration which ties together the context type and the record type.
/// </summary>
public class RecordRegistration<TContext, TRecord> : RecordRegistration<TContext>
    where TRecord : new()
{
    private readonly Func<TContext, IList<TRecord>> _collectionAccessor;

    public RecordRegistration(string commandName,
        Func<TContext, IList<TRecord>> collectionAccessor,
        PropertyInfo uniqueKeyProperty,
        List<PropertyInfo> positionalProperties)
        : base(commandName, typeof(TRecord), uniqueKeyProperty, positionalProperties)
    {
        _collectionAccessor = collectionAccessor;
    }

    /// <inheritdoc />
    public override object FindOrCreateRecord(TContext context, string uniqueKey)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Retrieve the collection from the context.
        var collection = _collectionAccessor(context);

        // Try to find an existing record (by comparing the unique key).
        TRecord? record = default;
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

        if (record is null)
        {
            // Create a new record if none exists.
            record = new();
            UniqueKeyProperty.SetValue(record, uniqueKey);
            collection.Add(record);
        }

        return record;
    }
}