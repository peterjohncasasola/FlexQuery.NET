using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Language-agnostic intermediate representation of an expand query.
/// <para>
/// This is the contract between parsers (FQL, DSL, etc.) and the <see cref="ExpandNormalizer"/>.
/// Parsers produce this shape; the normalizer consumes it. Neither side knows about the other's syntax.
/// </para>
/// </summary>
internal sealed class ExpandAst
{
    /// <summary>Navigation path segments for this expand level.</summary>
    public List<string> Path { get; set; } = [];

    /// <summary>Optional filter expression applied to the navigation collection.</summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>Optional sort expressions applied to the navigation collection.</summary>
    public List<SortNode> Sort { get; set; } = [];

    /// <summary>Optional number of items to take.</summary>
    public int? Take { get; set; }

    /// <summary>Nested expand blocks on child navigations.</summary>
    public List<ExpandAst> Children { get; set; } = [];
}
