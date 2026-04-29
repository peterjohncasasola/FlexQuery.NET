namespace DynamicQueryable.Exceptions;

/// <summary>
/// Thrown when a sort instruction references a property that does not exist on the entity type.
/// </summary>
public sealed class InvalidSortFieldException : Exception
{
    /// <summary>
    /// Gets the name of the field that caused the exception.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// Gets the type of the entity that the invalid field was referenced against.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidSortFieldException"/> class.
    /// </summary>
    /// <param name="fieldName">The name of the invalid field.</param>
    /// <param name="entityType">The type of the entity.</param>
    public InvalidSortFieldException(string fieldName, Type entityType)
        : base($"Sort field '{fieldName}' does not exist on type '{entityType.Name}'.")
    {
        FieldName = fieldName;
        EntityType = entityType;
    }
}
