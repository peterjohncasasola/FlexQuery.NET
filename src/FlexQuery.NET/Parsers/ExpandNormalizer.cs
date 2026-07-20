using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Converts the language-agnostic <see cref="ExpandAst"/> into the canonical
/// recursive <see cref="IncludeNode"/> tree used by validators and providers.
/// <para>
/// This normalizer is syntax-agnostic: it knows nothing about FQL, DSL, or any other
/// query language. It operates solely on the <see cref="ExpandAst"/> contract.
/// </para>
/// </summary>
internal static class ExpandNormalizer
{
    /// <summary>
    /// Normalizes a list of <see cref="ExpandAst"/> roots into <see cref="IncludeNode"/> trees.
    /// </summary>
    public static List<IncludeNode> Normalize(IReadOnlyList<ExpandAst> astRoots)
    {
        ArgumentNullException.ThrowIfNull(astRoots);
        var result = new List<IncludeNode>(astRoots.Count);
        foreach (var ast in astRoots)
        {
            var node = NormalizeNode(ast);
            result.Add(node);
        }
        return result;
    }

    /// <summary>
    /// Normalizes a single <see cref="ExpandAst"/> node into an <see cref="IncludeNode"/>.
    /// Flat dotted paths are expanded into recursive children.
    /// Filter, sort, and take are attached to the deepest node in the path.
    /// </summary>
    private static IncludeNode NormalizeNode(ExpandAst ast)
    {
        ArgumentNullException.ThrowIfNull(ast);

        if (ast.Path.Count == 0)
            throw new InvalidOperationException("Expand node has no path segments.");

        if (ast.Path.Count == 1)
        {
            return new IncludeNode
            {
                Path = ast.Path[0],
                Filter = ast.Filter,
                Sort = ast.Sort.Count > 0 ? ast.Sort : null,
                Take = ast.Take,
                Children = Normalize(ast.Children)
            };
        }

        // Multiple path segments: create recursive tree from flat path
        // e.g. Path = ["Orders", "OrderItems"] becomes:
        // IncludeNode { Path = "Orders", Children = [ IncludeNode { Path = "OrderItems", Filter = ..., Sort = ..., Take = ... } ] }
        var first = new IncludeNode
        {
            Path = ast.Path[0],
            Children = [CreateDeepNode(ast.Path, 1, ast.Filter, ast.Sort, ast.Take, ast.Children)]
        };

        return first;
    }

    /// <summary>
    /// Creates a recursive IncludeNode chain from remaining path segments,
    /// attaching the filter/sort/take to the deepest node.
    /// </summary>
    private static IncludeNode CreateDeepNode(
        List<string> pathSegments,
        int index,
        FilterGroup? filter,
        List<SortNode> sort,
        int? take,
        List<ExpandAst> additionalChildren)
    {
        if (index >= pathSegments.Count - 1)
        {
            // Deepest segment: attach filter/sort/take here
            return new IncludeNode
            {
                Path = pathSegments[index],
                Filter = filter,
                Sort = sort.Count > 0 ? sort : null,
                Take = take,
                Children = Normalize(additionalChildren)
            };
        }

        // Intermediate segment: recurse
        return new IncludeNode
        {
            Path = pathSegments[index],
            Children = [CreateDeepNode(pathSegments, index + 1, filter, sort, take, additionalChildren)]
        };
    }

    /// <summary>
    /// Normalizes a list of child <see cref="ExpandAst"/> nodes into <see cref="IncludeNode"/> children.
    /// </summary>
    private static List<IncludeNode> Normalize(List<ExpandAst> children)
    {
        var result = new List<IncludeNode>(children.Count);
        foreach (var child in children)
        {
            result.Add(NormalizeNode(child));
        }
        return result;
    }
}
