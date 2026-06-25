using FlexQuery.NET.Models;

namespace FlexQuery.NET.Projection;

public sealed class ProjectionMetadata
{
    public bool IsProjected => FieldTypes.Count > 0;

    public Type EntityType { get; }

    public SelectionNode SelectTree { get; }

    public IReadOnlyDictionary<string, Type> FieldTypes { get; }

    internal ProjectionMetadata(
        Type entityType,
        SelectionNode selectTree,
        IReadOnlyDictionary<string, Type> fieldTypes)
    {
        EntityType = entityType;
        SelectTree = selectTree;
        FieldTypes = fieldTypes;
    }
}
