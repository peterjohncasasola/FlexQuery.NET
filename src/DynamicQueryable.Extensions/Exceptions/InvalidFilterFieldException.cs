namespace DynamicQueryable.Exceptions;

/// <summary>
/// Thrown when a filter condition references a property that does not exist on the entity type.
/// </summary>
public sealed class InvalidFilterFieldException : Exception
{
    public string FieldName { get; }
    public Type EntityType { get; }

    public InvalidFilterFieldException(string fieldName, Type entityType)
        : base($"Field '{fieldName}' does not exist on type '{entityType.Name}'.")
    {
        FieldName = fieldName;
        EntityType = entityType;
    }
}
