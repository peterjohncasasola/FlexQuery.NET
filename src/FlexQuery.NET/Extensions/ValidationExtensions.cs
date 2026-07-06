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
    /// <param name="options">The query options to validate.</param>
    /// <param name="entityType">The entity type being queried.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    public static ValidationResult Validate(this QueryOptions options, Type entityType)
        => options.Validate(entityType, new QueryExecutionOptions());

    /// <summary>
    /// Validates the query options using the default validation pipeline and specified execution rules.
    /// </summary>
    /// <param name="options">The query options to validate.</param>
    /// <param name="entityType">The entity type being queried.</param>
    /// <param name="execOptions">The execution options defining server-side constraints.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    public static ValidationResult Validate(this QueryOptions options, Type entityType, QueryExecutionOptions execOptions)
    {
        var context = new QueryContext { TargetType = entityType, ExecutionOptions = execOptions };
        return _defaultValidator.Validate(options, context);
    }

    /// <summary>
    /// Validates the query options using the default validation pipeline.
    /// </summary>
    /// <typeparam name="T">The entity type being queried.</typeparam>
    /// <param name="query">The source queryable (used for type inference).</param>
    /// <param name="options">The query options to validate.</param>
    /// <param name="execOptions">The execution options defining server-side constraints.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    public static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options, QueryExecutionOptions execOptions)
        => query.Validate(options, execOptions, _defaultValidator);

    
    /// <summary>
    /// Validates the query options using a specific validator.
    /// </summary>
    /// <typeparam name="T">The entity type being queried.</typeparam>
    /// <param name="query">The source queryable (used for type inference).</param>
    /// <param name="options">The query options to validate.</param>
    /// <param name="execOptions">The execution options defining server-side constraints.</param>
    /// <param name="validator">The validator to use.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="validator"/> is null.</exception>
    internal static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options, QueryExecutionOptions execOptions, IQueryValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var context = new QueryContext { TargetType = typeof(T), ExecutionOptions = execOptions };
        return validator.Validate(options, context);
    }

}

