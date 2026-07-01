using System.Collections.ObjectModel;

namespace FlexQuery.NET.Models;

/// <summary>
/// Represents a merged field/navigation selection tree.
/// </summary>
public sealed class SelectionNode
{
    private readonly Dictionary<string, SelectionNode> _children = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when this node should include the default scalar payload for its CLR type.
    /// Used for include-style navigation expansion.
    /// </summary>
    public bool IncludeAllScalars { get; private set; }

    /// <summary>Gets a read-only view of the child selection nodes, keyed by property name.</summary>
    public IReadOnlyDictionary<string, SelectionNode> Children => new ReadOnlyDictionary<string, SelectionNode>(_children);

    /// <summary>Gets the number of direct child selection nodes.</summary>
    public int Count => _children.Count;

    /// <summary>Gets whether this node has any child selection nodes.</summary>
    public bool HasChildren => _children.Count > 0;
    
    /// <summary>
    /// Optional filter to apply when projecting this collection navigation.
    /// </summary>
    public FilterGroup? Filter { get; set; }

    /// <summary>
    /// Optional alias name used for the projected property (e.g. "productName as name").
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>Gets or creates a child selection node for the specified name.</summary>
    /// <param name="name">The property name for the child node.</param>
    /// <returns>An existing or newly created <see cref="SelectionNode"/>.</returns>
    public SelectionNode GetOrAddChild(string name)
    {
        if (!_children.TryGetValue(name, out var child))
        {
            child = new SelectionNode();
            _children[name] = child;
        }

        return child;
    }

    /// <summary>Attempts to retrieve a child selection node by name.</summary>
    /// <param name="name">The property name of the child node.</param>
    /// <param name="child">When this method returns, contains the child node if found.</param>
    /// <returns>true if the child was found; otherwise, false.</returns>
    public bool TryGetChild(string name, out SelectionNode child)
        => _children.TryGetValue(name, out child!);

    /// <summary>Enumerates all direct child selection nodes.</summary>
    public IEnumerable<KeyValuePair<string, SelectionNode>> EnumerateChildren()
        => _children;

    /// <summary>Marks this node to include all scalar properties of its associated type.</summary>
    public void MarkIncludeAllScalars() => IncludeAllScalars = true;

    /// <summary>Clears the include-all-scalars flag on this node.</summary>
    public void ClearIncludeAllScalars() => IncludeAllScalars = false;

    /// <summary>Removes a child selection node by name.</summary>
    /// <param name="name">The property name of the child node to remove.</param>
    public void RemoveChild(string name) => _children.Remove(name);
}
