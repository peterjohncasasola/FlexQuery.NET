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

}
