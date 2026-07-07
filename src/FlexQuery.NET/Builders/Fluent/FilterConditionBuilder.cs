using System.Globalization;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Builders.Fluent;

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
        return value switch
        {
            null => null,
            string s => s,
            DateTime dateTime => dateTime.ToString("o", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}