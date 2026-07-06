using FlexQuery.NET.Models;

namespace FlexQuery.NET.Projection;

/// <summary>
/// Contains metadata about a query projection, including the entity type,
/// the selection tree, and the resolved field-to-CLR-type mappings.
/// </summary>
internal sealed class ProjectionMetadata
{
    /// <summary>Whether this metadata represents an active projection with fields.</summary>
    public bool IsProjected => FieldTypes.Count > 0;

    /// <summary>The CLR type of the entity being projected.</summary>
    public Type EntityType { get; }

    /// <summary>The merged selection tree describing the projected fields and navigations.</summary>
    public SelectionNode SelectTree { get; }

    /// <summary>Maps projected output field names to their CLR types.</summary>
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
