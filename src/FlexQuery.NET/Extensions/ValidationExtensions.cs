using FlexQuery.NET.Models;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET;

/// <summary>
/// Extension methods for validating <see cref="QueryOptions"/>.
/// </summary>
public static class ValidationExtensions
{
    private static readonly IQueryValidator _defaultValidator = new QueryValidator();

    /// <summary>
    /// Validates the query options using the default validation pipeline.
    /// </summary>
    public static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options)
        => query.Validate(options, _defaultValidator);

    /// <summary>
    /// Validates the query options using a specific validator.
    /// </summary>
    public static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options, IQueryValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var context = new QueryContext { TargetType = typeof(T) };
        return validator.Validate(options, context);
    }

    /// <summary>
    /// Validates and applies the query options in a single step.
    /// Throws <see cref="QueryValidationException"/> if validation fails.
    /// </summary>
    public static IQueryable<T> ApplyValidatedQueryOptions<T>(this IQueryable<T> query, QueryOptions options)
    {
        var result = query.Validate(options);
        if (!result.IsValid)
        {
            throw new QueryValidationException(result);
        }
        return query.ApplyQueryOptions(options);
    }
}
