namespace DynamicQueryable.Exceptions;

/// <summary>
/// Thrown when a sort instruction references a property that does not exist on the entity type.
/// </summary>
public sealed class InvalidSortFieldException : Exception
{
    public string FieldName { get; }
    public Type EntityType { get; }

    public InvalidSortFieldException(string fieldName, Type entityType)
        : base($"Sort field '{fieldName}' does not exist on type '{entityType.Name}'.")
    {
        FieldName = fieldName;
        EntityType = entityType;
    }
}
