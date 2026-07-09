using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Builders.Fluent;

/// <summary>Fluent builder for constructing <see cref="QueryOptions"/> instances using a chained API.</summary>
public sealed class FluentQueryBuilder
{
    private readonly QueryOptions _options = new();

    /// <summary>Builds and returns the configured <see cref="QueryOptions"/>.</summary>
    public QueryOptions Build() => _options;

    /// <summary>Implicitly converts a <see cref="FluentQueryBuilder"/> to <see cref="QueryOptions"/>.</summary>
    public static implicit operator QueryOptions?(FluentQueryBuilder? builder) => builder?._options;

    /// <summary>Sets the filter condition using a <see cref="FilterGroupBuilder"/> lambda.</summary>
    public FluentQueryBuilder Filter(Action<FilterGroupBuilder> configure)
    {
        var builder = new FilterGroupBuilder();
        configure(builder);
        _options.Filter = builder.Build();
        return this;
    }

    /// <summary>Adds sort expressions using a <see cref="SortBuilder"/> lambda.</summary>
    public FluentQueryBuilder Sort(Action<SortBuilder> configure)
    {
        var builder = new SortBuilder();
        configure(builder);
        _options.Sort = builder.Build();
        return this;
    }

    /// <summary>Sets the selected fields (replaces any previous selection).</summary>
    public FluentQueryBuilder Select(params string[] fields)
    {
        _options.Select = fields.Length > 0 ? [..fields] : null;
        return this;
    }

    /// <summary>Sets simple navigation includes (replaces any previous includes).</summary>
    public FluentQueryBuilder Include(params string[] paths)
    {
        _options.Includes = paths.Length > 0 ? [..paths] : null;
        return this;
    }

    /// <summary>Adds filtered navigation expansion trees using an <see cref="ExpandBuilder"/> lambda.</summary>
    public FluentQueryBuilder Expand(Action<ExpandBuilder> configure)
    {
        var builder = new ExpandBuilder();
        configure(builder);
        var includes = builder.Build();
        if (includes.Count <= 0) return this;
        _options.Expand ??= [];
        _options.Expand.AddRange(includes);
        return this;
    }

    /// <summary>Sets the projection mode (Nested, Flat, or FlatMixed).</summary>
    public FluentQueryBuilder Mode(ProjectionMode mode)
    {
        _options.ProjectionMode = mode;
        return this;
    }

    /// <summary>Sets the group-by fields (replaces any previous group-by).</summary>
    public FluentQueryBuilder GroupBy(params string[] fields)
    {
        _options.GroupBy = fields.Length > 0 ? [..fields] : null;
        return this;
    }

    /// <summary>Adds aggregate projections using an <see cref="AggregateBuilder"/> lambda.</summary>
    public FluentQueryBuilder Aggregate(Action<AggregateBuilder> configure)
    {
        var builder = new AggregateBuilder();
        configure(builder);
        _options.Aggregates.AddRange(builder.Build());
        return this;
    }

    /// <summary>Sets the HAVING condition for aggregate filtering.</summary>
    public FluentQueryBuilder Having(string function, string? field, string op, string? value)
    {
        _options.Having = new HavingCondition
        {
            Function = function,
            Field = field,
            Operator = op,
            Value = value
        };
        return this;
    }

    /// <summary>Enables or disables the DISTINCT clause.</summary>
    public FluentQueryBuilder Distinct(bool value = true)
    {
        _options.Distinct = value;
        return this;
    }

    /// <summary>Sets the page number and page size for pagination.</summary>
    public FluentQueryBuilder Page(int page, int pageSize)
    {
        _options.Paging.Page = page;
        _options.Paging.PageSize = pageSize;
        _options.OffsetExplicitlyRequested = true;
        return this;
    }
    
    /// <summary>
    /// Enables keyset pagination using the specified page size and optional cursor token.
    /// </summary>
    /// <param name="pageSize">
    /// The maximum number of records to return.
    /// </param>
    /// <param name="cursor">
    /// The cursor token representing the starting position for the next or previous page.
    /// Pass <see langword="null"/> to retrieve the first page.
    /// </param>
    /// <returns>
    /// The current <see cref="FluentQueryBuilder"/> instance for method chaining.
    /// </returns>
    public FluentQueryBuilder UseKeysetPagination(int pageSize, string? cursor = null)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                "Page size must be greater than zero.");
        
        _options.IsKeysetMode = true;
        _options.Cursor = cursor is null ? null : new KeysetCursor(cursor);
        _options.Paging.PageSize = pageSize;
        _options.OffsetExplicitlyRequested = false;
        return this;
    }

    /// <summary>Disables paging entirely.</summary>
    public FluentQueryBuilder DisablePaging()
    {
        _options.Paging.Disabled = true;
        return this;
    }
}
