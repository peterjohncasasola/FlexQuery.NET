using FlexQuery.NET.Models;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;

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
    public static ValidationResult Validate(this QueryOptions options, Type entityType, QueryGovernanceOptions execOptions)
    {
        var context = new QueryContext { TargetType = entityType, ExecutionOptions = execOptions };
        return _defaultValidator.Validate(options, context);
    }

    /// <summary>
    /// Validates the query options using the default validation pipeline, specified execution rules,
    /// and a pre-built context that may carry additional state such as <see cref="QuerySurface"/>.
    /// </summary>
    /// <param name="options">The query options to validate.</param>
    /// <param name="context">The pre-built query context.</param>
    /// <param name="execOptions">The execution options defining server-side constraints.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static ValidationResult ValidateOrThrow(
        this QueryOptions options,
        QueryContext context,
        QueryGovernanceOptions? execOptions = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        execOptions ??= new QueryExecutionOptions();

        if (execOptions.ExpressionMappings != null)
        {
            options.Items[ContextKeys.ExpressionMappings] = execOptions.ExpressionMappings;
        }

        // Publish the active DTO surface so every query operation (filter, sort, group,
        // aggregate, projection, keyset paging) resolves public field names through the
        // same QuerySurface/FieldDescriptor abstraction.
        if (context.QuerySurface != null)
        {
            options.Items[ContextKeys.QuerySurface] = context.QuerySurface;
        }

        context.ExecutionOptions ??= execOptions;
        var result = _defaultValidator.Validate(options, context);

        if (!result.IsValid && execOptions.StrictFieldValidation)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Message));
            throw new QueryValidationException(result);
        }

        return result;
    }

    /// <summary>
    /// Validates the query options using the default validation pipeline.
    /// </summary>
    /// <typeparam name="T">The entity type being queried.</typeparam>
    /// <param name="query">The source queryable (used for type inference).</param>
    /// <param name="options">The query options to validate.</param>
    /// <param name="execOptions">The execution options defining server-side constraints.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    public static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options, QueryGovernanceOptions execOptions)
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
    internal static ValidationResult Validate<T>(this IQueryable<T> query, QueryOptions options, QueryGovernanceOptions execOptions, IQueryValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var context = new QueryContext { TargetType = typeof(T), ExecutionOptions = execOptions };
        return validator.Validate(options, context);
    }

}
