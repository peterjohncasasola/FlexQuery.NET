using FlexQuery.NET.Constants;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders.Fluent;

/// <summary>Builds a FilterGroup using direct filter methods. Standalone class.</summary>
public sealed class FilterGroupBuilder
{
    private readonly FilterGroup _group = new() { Logic = LogicOperator.And };

    /// <summary>Builds and returns the configured <see cref="FilterGroup"/>.</summary>
    public FilterGroup Build() => _group;

    /// <summary>Adds an equality condition: field == value.</summary>
    public FilterGroupBuilder Equal(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.Equal, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds a not-equal condition: field != value.</summary>
    public FilterGroupBuilder NotEqual(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.NotEqual, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds a greater-than condition: field &gt; value.</summary>
    public FilterGroupBuilder GreaterThan(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.GreaterThan, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds a greater-than-or-equal condition: field &gt;= value.</summary>
    public FilterGroupBuilder GreaterThanOrEqual(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.GreaterThanOrEq, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds a less-than condition: field &lt; value.</summary>
    public FilterGroupBuilder LessThan(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.LessThan, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds a less-than-or-equal condition: field &lt;= value.</summary>
    public FilterGroupBuilder LessThanOrEqual(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.LessThanOrEq, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds a substring containment condition: field contains value.</summary>
    public FilterGroupBuilder Contains(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.Contains, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds a prefix match condition: field starts with value.</summary>
    public FilterGroupBuilder StartsWith(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.StartsWith, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds a suffix match condition: field ends with value.</summary>
    public FilterGroupBuilder EndsWith(string field, object? value)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.EndsWith, Value = FilterValueFormatter.Format(value) });
        return this;
    }

    /// <summary>Adds an inclusion condition: field IN (values).</summary>
    public FilterGroupBuilder In(string field, params object?[] values)
    {
        var joined = string.Join(",", values.Select(FilterValueFormatter.Format));
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.In, Value = joined });
        return this;
    }

    /// <summary>Adds an exclusion condition: field NOT IN (values).</summary>
    public FilterGroupBuilder NotIn(string field, params object?[] values)
    {
        var joined = string.Join(",", values.Select(FilterValueFormatter.Format));
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.NotIn, Value = joined });
        return this;
    }

    /// <summary>Adds a null check condition: field IS NULL.</summary>
    public FilterGroupBuilder IsNull(string field)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.IsNull });
        return this;
    }

    /// <summary>Adds a not-null condition: field IS NOT NULL.</summary>
    public FilterGroupBuilder IsNotNull(string field)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.IsNotNull });
        return this;
    }

    /// <summary>Adds a range condition: field BETWEEN min AND max.</summary>
    public FilterGroupBuilder Between(string field, object? min, object? max)
    {
        _group.Filters.Add(new FilterCondition { Field = field, Operator = FilterOperators.Between, Value = $"{FilterValueFormatter.Format(min)},{FilterValueFormatter.Format(max)}" });
        return this;
    }

    /// <summary>Starts a nested AND group.</summary>
    public FilterGroupBuilder And(Action<FilterGroupBuilder> configure)
    {
        var nested = new FilterGroupBuilder();
        configure(nested);
        nested._group.Logic = LogicOperator.And;
        _group.Groups.Add(nested._group);
        return this;
    }

    /// <summary>Starts a nested OR group.</summary>
    public FilterGroupBuilder Or(Action<FilterGroupBuilder> configure)
    {
        var nested = new FilterGroupBuilder();
        configure(nested);
        nested._group.Logic = LogicOperator.Or;
        _group.Groups.Add(nested._group);
        return this;
    }
}
