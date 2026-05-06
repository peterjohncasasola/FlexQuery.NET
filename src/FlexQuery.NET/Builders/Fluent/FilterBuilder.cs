using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders;

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
public class FilterBuilder<T> : FilterBuilder
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

/// <summary>
/// Builds a filter condition and returns control to the parent builder.
/// </summary>
public sealed class FilterConditionBuilder<TParent>
    where TParent : FilterBuilder
{
    private readonly TParent _parent;
    private readonly string _field;
    private readonly LogicOperator _logic;
    private bool _negated;

    internal FilterConditionBuilder(TParent parent, string field, LogicOperator logic)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _field = string.IsNullOrWhiteSpace(field)
            ? throw new ArgumentException("Field name cannot be null or whitespace.", nameof(field))
            : field;
        _logic = logic;
    }

    /// <summary>
    /// Negates the next condition.
    /// </summary>
    public FilterConditionBuilder<TParent> Not()
    {
        _negated = !_negated;
        return this;
    }

        /// <summary>
        /// Adds an equality condition for the specified field value.
        /// </summary>
        /// <param name="value">The expected value to match.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent Eq(object? value) => FinalizeCondition("eq", FormatValue(value));

        /// <summary>
        /// Adds a not-equality condition for the specified field value.
        /// </summary>
        /// <param name="value">The value that should not be matched.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent Neq(object? value) => FinalizeCondition("neq", FormatValue(value));

        /// <summary>
        /// Adds a substring containment condition for the specified field value.
        /// </summary>
        /// <param name="value">The substring to search for within the field value.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent Contains(object? value) => FinalizeCondition("contains", FormatValue(value));

        /// <summary>
        /// Adds a prefix match condition for the specified field value.
        /// </summary>
        /// <param name="value">The prefix that the field value should start with.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent StartsWith(object? value) => FinalizeCondition("startswith", FormatValue(value));

        /// <summary>
        /// Adds a suffix match condition for the specified field value.
        /// </summary>
        /// <param name="value">The suffix that the field value should end with.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent EndsWith(object? value) => FinalizeCondition("endswith", FormatValue(value));

        /// <summary>
        /// Adds a greater-than comparison condition for the specified field value.
        /// </summary>
        /// <param name="value">The threshold value that the field value must exceed.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent GreaterThan(object? value) => FinalizeCondition("gt", FormatValue(value));

        /// <summary>
        /// Adds a greater-than-or-equal comparison condition for the specified field value.
        /// </summary>
        /// <param name="value">The threshold value that the field value must meet or exceed.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent GreaterThanOrEqual(object? value) => FinalizeCondition("gte", FormatValue(value));

        /// <summary>
        /// Adds a less-than comparison condition for the specified field value.
        /// </summary>
        /// <param name="value">The threshold value that the field value must be less than.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent LessThan(object? value) => FinalizeCondition("lt", FormatValue(value));

        /// <summary>
        /// Adds a less-than-or-equal comparison condition for the specified field value.
        /// </summary>
        /// <param name="value">The threshold value that the field value must be less than or equal to.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent LessThanOrEqual(object? value) => FinalizeCondition("lte", FormatValue(value));

        /// <summary>
        /// Adds an inclusion condition checking if the field value is one of the specified values.
        /// </summary>
        /// <param name="values">The set of acceptable values for the field.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent In(params object?[] values) => FinalizeCondition("in", string.Join(",", values.Select(FormatValue)));

        /// <summary>
        /// Adds a range condition checking if the field value falls between the specified minimum and maximum.
        /// </summary>
        /// <param name="min">The inclusive lower bound of the range.</param>
        /// <param name="max">The inclusive upper bound of the range.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent Between(object? min, object? max) => FinalizeCondition("between", string.Join(",", FormatValue(min), FormatValue(max)));

        /// <summary>
        /// Adds a null check condition for the specified field.
        /// </summary>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent IsNull() => FinalizeCondition("isnull", null);

        /// <summary>
        /// Adds a not-null check condition for the specified field.
        /// </summary>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent NotNull() => FinalizeCondition("notnull", null);

        /// <summary>
        /// Adds a scoped filter condition that matches if ANY of the nested filter's conditions are satisfied.
        /// </summary>
        /// <param name="configure">An action that configures the nested filter conditions.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent Any(Action<FilterBuilder> configure)
        {
            var nested = new FilterBuilder();
            configure(nested);
            return FinalizeCondition("any", null, nested.Build());
        }

        /// <summary>
        /// Adds a scoped filter condition that matches if ALL of the nested filter's conditions are satisfied.
        /// </summary>
        /// <param name="configure">An action that configures the nested filter conditions.</param>
        /// <returns>The parent builder for fluent chaining.</returns>
        public TParent All(Action<FilterBuilder> configure)
        {
            var nested = new FilterBuilder();
            configure(nested);
            return FinalizeCondition("all", null, nested.Build());
        }

    private TParent FinalizeCondition(string @operator, string? value, FilterGroup? scopedFilter = null)
    {
        var condition = new FilterCondition
        {
            Field = _field,
            Operator = @operator,
            Value = value,
            IsNegated = _negated,
            ScopedFilter = scopedFilter
        };

        _parent.AddCondition(condition, _logic);
        return _parent;
    }

    private static string? FormatValue(object? value)
    {
        if (value is null)
            return null;

        if (value is string s)
            return s;

        if (value is DateTime dateTime)
            return dateTime.ToString("o", CultureInfo.InvariantCulture);

        if (value is bool boolean)
            return boolean ? "true" : "false";

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString();
    }
}
