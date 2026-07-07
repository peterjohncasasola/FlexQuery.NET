namespace FlexQuery.NET.EntityFrameworkCore.Includes;

/// <summary>
/// Identifies which EF Core <c>Include</c> / <c>ThenInclude</c> overload
/// family applies at the current point in an include-tree walk.
/// </summary>
internal enum IncludeContext
{
    /// <summary>Reached via <c>query.Include(...)</c>.</summary>
    Root,

    /// <summary>Reached via <c>.ThenInclude(...)</c> after a <b>collection</b> navigation.</summary>
    AfterCollection,

    /// <summary>Reached via <c>.ThenInclude(...)</c> after a <b>reference</b> navigation.</summary>
    AfterReference
}