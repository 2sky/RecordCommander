using System.Collections;
using System.Reflection;

namespace RecordCommander;

/// <summary>
/// Generic registration which ties together the context type and the record type.
/// </summary>
public class RecordRegistration<TContext, TRecord> : RecordRegistrationBase
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

    /// <summary>
    /// Retrieves the collection (for TRecord) from the given context.
    /// </summary>
    public override IList GetCollection(object context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context is not TContext ctx)
            throw new ArgumentException("Invalid context type", nameof(context));

        return (IList)_collectionAccessor(ctx);
    }
}