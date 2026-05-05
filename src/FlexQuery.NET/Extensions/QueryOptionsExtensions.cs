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
    public static bool HasProjection(this QueryOptions options)
    {
        return options.Select != null && options.Select.Count > 0;
    }
}