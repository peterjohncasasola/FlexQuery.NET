using System;
using System.Linq;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;

namespace FlexQuery.NET;

/// <summary>
/// Fluent API extensions that construct <see cref="QueryOptions"/> filters using builder delegates.
/// </summary>
public static class FluentQueryExtensions
{
    /// <summary>
    /// Builds and applies a fluent filter to an existing <see cref="QueryOptions"/> instance.
    /// </summary>
    /// <param name="options">The query options to modify.</param>
    /// <param name="configure">A function that configures a filter builder and returns it.</param>
    /// <returns>The modified <see cref="QueryOptions"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="configure"/> is null.</exception>
    public static QueryOptions Filter(this QueryOptions options, Func<FilterBuilder, FilterBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = configure(new FilterBuilder());
        options.Filter = builder?.Build();
        return options;
    }

    /// <summary>
    /// Builds and applies a fluent filter to an existing <see cref="QueryOptions"/> instance.
    /// </summary>
    /// <param name="options">The query options to modify.</param>
    /// <param name="configure">An action that configures a filter builder.</param>
    /// <returns>The modified <see cref="QueryOptions"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="configure"/> is null.</exception>
    public static QueryOptions Filter(this QueryOptions options, Action<FilterBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new FilterBuilder();
        configure(builder);
        options.Filter = builder.Build();
        return options;
    }

    /// <summary>
    /// Builds and applies a strongly-typed fluent filter to an existing <see cref="QueryOptions"/> instance.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="options">The query options to modify.</param>
    /// <param name="configure">A function that configures a typed filter builder and returns it.</param>
    /// <returns>The modified <see cref="QueryOptions"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="configure"/> is null.</exception>
    public static QueryOptions Filter<T>(this QueryOptions options, Func<FilterBuilder<T>, FilterBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = configure(new FilterBuilder<T>());
        options.Filter = builder?.Build();
        return options;
    }

    /// <summary>
    /// Builds and applies a strongly-typed fluent filter to an existing <see cref="QueryOptions"/> instance.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="options">The query options to modify.</param>
    /// <param name="configure">An action that configures a typed filter builder.</param>
    /// <returns>The modified <see cref="QueryOptions"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="configure"/> is null.</exception>
    public static QueryOptions Filter<T>(this QueryOptions options, Action<FilterBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new FilterBuilder<T>();
        configure(builder);
        options.Filter = builder.Build();
        return options;
    }

    /// <summary>
    /// Applies a fluent filter to an <see cref="IQueryable{T}"/> and returns a filtered queryable.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="configure">A function that configures a typed filter builder and returns it.</param>
    /// <returns>A filtered <see cref="IQueryable{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> or <paramref name="configure"/> is null.</exception>
    public static IQueryable<T> Filter<T>(this IQueryable<T> query, Func<FilterBuilder<T>, FilterBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = configure(new FilterBuilder<T>());
        var options = new QueryOptions { Filter = builder?.Build() };
        return query.ApplyFilter(options);
    }

    /// <summary>
    /// Applies a fluent filter to an <see cref="IQueryable{T}"/> and returns a filtered queryable.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="configure">An action that configures a typed filter builder.</param>
    /// <returns>A filtered <see cref="IQueryable{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> or <paramref name="configure"/> is null.</exception>
    public static IQueryable<T> Filter<T>(this IQueryable<T> query, Action<FilterBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new FilterBuilder<T>();
        configure(builder);
        var options = new QueryOptions { Filter = builder.Build() };
        return query.ApplyFilter(options);
    }
}

