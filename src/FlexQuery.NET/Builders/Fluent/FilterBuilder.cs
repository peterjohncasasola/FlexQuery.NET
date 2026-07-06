using System.Linq.Expressions;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders.Fluent;

/// <summary>
/// Builds a structured <see cref="FilterGroup"/> using a fluent API.
/// </summary>
public class FilterBuilder
{
    private readonly FilterGroup _root = new();

    /// <summary>
    /// Creates the constructed filter group.
    /// </summary>
    public FilterGroup Build() => _root;

    /// <summary>
    /// Begins a condition for the provided field name.
    /// </summary>
    public FilterConditionBuilder<FilterBuilder> Field(string field) => new(this, field, LogicOperator.And);

    /// <summary>
    /// Begins an AND condition for the provided field name.
    /// </summary>
    public FilterConditionBuilder<FilterBuilder> And(string field) => new(this, field, LogicOperator.And);

    /// <summary>
    /// Begins an OR condition for the provided field name.
    /// </summary>
    public FilterConditionBuilder<FilterBuilder> Or(string field) => new(this, field, LogicOperator.Or);

    /// <summary>
    /// Begins a nested AND group.
    /// </summary>
    public FilterBuilder AndGroup(Action<FilterBuilder> configure)
    {
        return AddGroup(configure, LogicOperator.And);
    }

    /// <summary>
    /// Begins a nested OR group.
    /// </summary>
    public FilterBuilder OrGroup(Action<FilterBuilder> configure)
    {
        return AddGroup(configure, LogicOperator.Or);
    }

    private FilterBuilder AddGroup(Action<FilterBuilder> configure, LogicOperator logic)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var nested = new FilterBuilder();
        configure(nested);
        var group = nested.Build();
        // Ensure the group's logic matches the intended group type (And/Or)
        group.Logic = logic;
        if ((group.Filters?.Count ?? 0) == 0 && (group.Groups?.Count ?? 0) == 0)
            return this;

        AddGroup(group, logic);
        return this;
    }

    internal void AddCondition(FilterCondition condition, LogicOperator logic)
    {
        if (condition is null)
            throw new ArgumentNullException(nameof(condition));

        if (_root.Filters.Count == 0 && _root.Groups.Count == 0)
        {
            _root.Logic = logic;
            _root.Filters.Add(condition);
            return;
        }

        if (logic != _root.Logic)
        {
            var previous = new FilterGroup
            {
                Logic = _root.Logic,
                IsNegated = _root.IsNegated,
                Filters = new List<FilterCondition>(_root.Filters),
                Groups = new List<FilterGroup>(_root.Groups)
            };

            _root.Filters = new List<FilterCondition>();
            _root.Groups = new List<FilterGroup>();
            _root.IsNegated = false;
            _root.Logic = logic;
            _root.Groups.Add(previous);
        }

        _root.Filters.Add(condition);
    }

    internal void AddGroup(FilterGroup group, LogicOperator logic)
    {
        if (group is null)
            throw new ArgumentNullException(nameof(group));

        if (_root.Filters.Count == 0 && _root.Groups.Count == 0)
        {
            _root.Logic = logic;
            _root.Groups.Add(group);
            return;
        }

        if (logic != _root.Logic)
        {
            // If the current root contains only a single group and no direct filters,
            // we can simply change the root's logic and add the new group directly,
            // preserving the existing group as a direct child.
            if (_root.Filters.Count == 0 && _root.Groups.Count == 1)
            {
                _root.Logic = logic;
                _root.Groups.Add(group);
                return;
            }

            var previous = new FilterGroup
            {
                Logic = _root.Logic,
                IsNegated = _root.IsNegated,
                Filters = new List<FilterCondition>(_root.Filters),
                Groups = new List<FilterGroup>(_root.Groups)
            };

            _root.Filters = new List<FilterCondition>();
            _root.Groups = new List<FilterGroup>();
            _root.IsNegated = false;
            _root.Logic = logic;
            _root.Groups.Add(previous);
        }

        _root.Groups.Add(group);
    }


}

/// <summary>
/// A typed filter builder that supports expression-based field selection.
/// </summary>
public sealed class FilterBuilder<T> : FilterBuilder
{
    /// <summary>
    /// Begins a strongly-typed condition for the provided property selector.
    /// </summary>
    public FilterConditionBuilder<FilterBuilder<T>> Field(Expression<Func<T, object?>> selector)
        => new(this, ResolveMemberPath(selector), LogicOperator.And);

    /// <summary>
    /// Begins a strongly-typed AND condition for the provided property selector.
    /// </summary>
    public FilterConditionBuilder<FilterBuilder<T>> And(Expression<Func<T, object?>> selector)
        => new(this, ResolveMemberPath(selector), LogicOperator.And);

    /// <summary>
    /// Begins a strongly-typed OR condition for the provided property selector.
    /// </summary>
    public FilterConditionBuilder<FilterBuilder<T>> Or(Expression<Func<T, object?>> selector)
        => new(this, ResolveMemberPath(selector), LogicOperator.Or);

    private static string ResolveMemberPath(Expression<Func<T, object?>> selector)
    {
        if (selector is null)
            throw new ArgumentNullException(nameof(selector));

        var expression = selector.Body;

        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        var parts = new Stack<string>();

        while (expression is MemberExpression member)
        {
            parts.Push(member.Member.Name);
            expression = member.Expression ?? throw new InvalidOperationException("Unable to resolve member expression.");
        }

        if (expression is not ParameterExpression)
            throw new ArgumentException("Expression must be a simple member access expression.", nameof(selector));

        return string.Join('.', parts);
    }
}