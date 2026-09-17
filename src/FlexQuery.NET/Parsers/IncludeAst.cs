using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Language-agnostic intermediate representation of an <c>include</c> query option.
/// <para>
/// This is the contract between parsers (FQL, DSL, etc.) and the <see cref="IncludeNormalizer"/>.
/// Parsers produce this shape; the normalizer consumes it. Neither side knows about the other's syntax.
/// </para>
/// </summary>
internal sealed class IncludeAst
{
    /// <summary>Navigation path segments for this include level.</summary>
    public List<string> Path { get; set; } = [];

    /// <summary>Optional filter expression applied to the navigation collection.</summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>Optional sort expressions applied to the navigation collection.</summary>
    public List<SortNode> Sort { get; set; } = [];

    /// <summary>Optional number of items to take.</summary>
    public int? Take { get; set; }

    /// <summary>Nested include blocks on child navigations.</summary>
    public List<IncludeAst> Children { get; set; } = [];
}
