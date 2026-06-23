namespace FlexQuery.NET.Adapters.AgGrid.Models;

/// <summary>
/// Controls the names of adapter-defined metadata fields added to SSRM group rows.
/// </summary>
/// <remarks>
/// These fields are not required properties of an AG Grid SSRM response row. They are emitted as
/// integration helpers for client-side callbacks such as <c>isServerSideGroup</c>,
/// <c>getServerSideGroupKey</c>, and <c>getChildCount</c>.
/// </remarks>
public sealed class AgGridResponseFieldOptions
{
    /// <summary>
    /// Adapter-defined metadata field indicating a row is a group row.
    /// </summary>
    public string GroupFlagFieldName { get; set; } = "group";

    /// <summary>
    /// Adapter-defined metadata field containing the group key for the current level.
    /// </summary>
    public string KeyFieldName { get; set; } = "key";

    /// <summary>
    /// Adapter-defined metadata field containing the grouped field name.
    /// </summary>
    public string FieldFieldName { get; set; } = "field";

    /// <summary>
    /// Adapter-defined metadata field containing the zero-based grouping level.
    /// </summary>
    public string LevelFieldName { get; set; } = "level";

    /// <summary>
    /// Adapter-defined metadata field indicating that expanding this group returns leaf rows.
    /// </summary>
    public string LeafGroupFieldName { get; set; } = "leafGroup";

    /// <summary>
    /// Adapter-defined metadata field containing the full route for the group.
    /// </summary>
    public string RouteFieldName { get; set; } = "groupKeys";

    /// <summary>
    /// Adapter-defined data field used by AG Grid's <c>getChildCount</c> callback.
    /// </summary>
    public string ChildCountFieldName { get; set; } = "childCount";

    /// <summary>
    /// Source field copied from grouped rows into <see cref="ChildCountFieldName"/>. When omitted,
    /// child counts are not emitted unless the row already contains the destination field.
    /// </summary>
    public string? ChildCountSourceField { get; set; }
}
