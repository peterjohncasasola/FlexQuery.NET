using FlexQuery.NET.Models;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Exceptions;
using System.ComponentModel;

namespace FlexQuery.NET;

/// <summary>
/// Extension methods for validating <see cref="QueryOptions"/>.
/// </summary>
public static class ValidationExtensions
{
    private static readonly IQueryValidator _defaultValidator = new QueryValidator();

    /// <summary>
    /// Validates the query options using the default validation pipeline and default execution rules.
    /// </summary>
    public static ValidationResult Validate(this QueryOptions options, Type entityType)
        => options.Validate(entityType, new QueryExecutionOptions());

    /// <summary>
    /// Validates the query options using the default validation pipeline and specified execution rules.
    /// </summary>
    public static ValidationResult Validate(this QueryOptions options, Type entityType, QueryExecutionOptions execOptions)
    {
        var context = new QueryContext { TargetType = entityType, ExecutionOptions = execOptions };
        return _defaultValidator.Validate(options, context);
    }

    /// <summary>
    /// Validates the query options using the default validation pipeline.
    /// </summary>
    public static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options, QueryExecutionOptions execOptions)
        => query.Validate(options, execOptions, _defaultValidator);

    
    /// <summary>
    /// Validates the query options using a specific validator.
    /// </summary>
    public static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options, QueryExecutionOptions execOptions, IQueryValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var context = new QueryContext { TargetType = typeof(T), ExecutionOptions = execOptions };
        return validator.Validate(options, context);
    }

    /// <summary>
    /// Validates the query options using the default validation pipeline.
    /// </summary>
    [Obsolete("Validate with default execution rules is deprecated. Use Validate with QueryExecutionOptions instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options)
        => query.Validate(options, _defaultValidator);

    /// <summary>
    /// Validates the query options using a specific validator.
    /// </summary>
    [Obsolete("Validate with IQueryValidator is deprecated. Use Validate with QueryExecutionOptions instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
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

    [Obsolete("ApplyValidatedQueryOptions is deprecated. Use FlexQueryParameters with FlexQuery(...) instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
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
