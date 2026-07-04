using FlexQuery.NET.Builders.Fluent;

namespace FlexQuery.NET;

/// <summary>Entry point for the fluent query builder API.</summary>
public static class Query
{
    /// <summary>Creates a new FluentQueryBuilder instance.</summary>
    public static FluentQueryBuilder Create() => new();
}
