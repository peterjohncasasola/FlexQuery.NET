using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Validation;
using System.Collections.Generic;

namespace FlexQuery.NET.Extensions;

/// <summary>
/// Extensions for <see cref="QueryOptions"/>.
/// </summary>
public static class QueryOptionsExtensions
{
    /// <summary>
    /// Validates the query options or throws a <see cref="QueryValidationException"/>.
    /// </summary>
    /// <typeparam name="T">The entity type that the query targets.</typeparam>
    /// <param name="options">The query options to validate.</param>
    /// <param name="execOptions">Optional execution options that define server-side constraints.</param>
    /// <exception cref="QueryValidationException">Thrown when validation fails.</exception>
    public static void ValidateOrThrow<T>(
        this QueryOptions options,
        QueryExecutionOptions? execOptions = null)
    {
        var result = ValidateInternal<T>(options, execOptions);

        if (!result.IsValid)
            throw new QueryValidationException(result);
    }

    /// <summary>
    /// Validates the query options and returns the result safely.
    /// </summary>
    /// <typeparam name="T">The entity type that the query targets.</typeparam>
    /// <param name="options">The query options to validate.</param>
    /// <param name="execOptions">Optional execution options that define server-side constraints.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    public static ValidationResult ValidateSafe<T>(
        this QueryOptions options,
        QueryExecutionOptions? execOptions = null)
    {
        return ValidateInternal<T>(options, execOptions);
    }

    private static ValidationResult ValidateInternal<T>(
        QueryOptions options,
        QueryExecutionOptions? execOptions)
    {
        execOptions ??= new QueryExecutionOptions();

        var result = options.Validate(typeof(T), execOptions);

        return result;
    }

    /// <summary>
    /// Wraps data into a <see cref="QueryResult{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the data collection.</typeparam>
    /// <param name="options">The query options that contributed to the result.</param>
    /// <param name="data">The collection of result items.</param>
    /// <param name="totalCount">Optional total count of all items before paging.</param>
    /// <returns>A <see cref="QueryResult{T}"/> containing the data and pagination metadata.</returns>
    public static QueryResult<T> BuildQueryResult<T>(this QueryOptions options, IEnumerable<T> data, int? totalCount = null)
    {
        return new QueryResult<T>
        {
            TotalCount = totalCount,
            Page       = options.Paging.Page,
            PageSize   = options.Paging.PageSize,
            Data       = data.ToList()
        };
    }

    /// <summary>
    /// Checks if the query options contain any explicit projection.
    /// </summary>
    /// <param name="options">The query options to check.</param>
    /// <returns><c>true</c> if the options specify a projection; otherwise, <c>false</c>.</returns>
    public static bool HasProjection(this QueryOptions options)
    {
        return options.Select != null && options.Select.Count > 0;
    }

    /// <summary>
    /// Normalizes the filter AST into a canonical form for deterministic cache keys
    /// and semantic comparison of equivalent expressions.
    /// </summary>
    /// <param name="options">The query options whose filter should be normalized.</param>
    /// <returns>The same <see cref="QueryOptions"/> instance with its filter normalized.</returns>
    public static QueryOptions Normalize(this QueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Filter = FilterNormalizer.Normalize(options.Filter);
        return options;
    }}