using System.Collections.ObjectModel;

namespace DynamicQueryable.Models;

/// <summary>
/// Represents a merged field/navigation selection tree.
/// </summary>
internal sealed class SelectionNode
{
    private readonly Dictionary<string, SelectionNode> _children = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when this node should include the default scalar payload for its CLR type.
    /// Used for include-style navigation expansion.
    /// </summary>
    public bool IncludeAllScalars { get; private set; }

    public IReadOnlyDictionary<string, SelectionNode> Children => new ReadOnlyDictionary<string, SelectionNode>(_children);

    public int Count => _children.Count;

    public bool HasChildren => _children.Count > 0;
    
    /// <summary>
    /// Optional filter to apply when projecting this collection navigation.
    /// </summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>
    /// Optional alias name used for the projected property (e.g. "productName as name").
    /// </summary>
    public string? Alias { get; set; }

    public SelectionNode GetOrAddChild(string name)
    {
        if (!_children.TryGetValue(name, out var child))
        {
            child = new SelectionNode();
            _children[name] = child;
        }

        return child;
    }

    public bool TryGetChild(string name, out SelectionNode child)
        => _children.TryGetValue(name, out child!);

    public IEnumerable<KeyValuePair<string, SelectionNode>> EnumerateChildren()
        => _children;

    public void MarkIncludeAllScalars() => IncludeAllScalars = true;
}
