using FlexQuery.NET.Models;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Validation;

/// <summary>
/// Pipeline-based validator for <see cref="QueryOptions"/>.
/// </summary>
public sealed class QueryValidator : IQueryValidator
{
    private readonly List<IValidationRule> _rules = [];

    /// <summary>
    /// Initializes a new validator with default rules (Field, Operator, Type).
    /// </summary>
    public QueryValidator()
    {
        _rules.Add(new FieldExistenceRule());
        _rules.Add(new OperatorValidityRule());
        _rules.Add(new TypeCompatibilityRule());
        _rules.Add(new FieldAccessValidator());
    }

    /// <summary>
    /// Adds a custom validation rule to the pipeline.
    /// </summary>
    public QueryValidator AddRule(IValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }

    /// <inheritdoc />
    public ValidationResult Validate(QueryOptions options, QueryContext context)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);

        var result = ValidationResult.Success();
        foreach (var rule in _rules)
        {
            rule.Validate(options, context, result);
        }
        return result;
    }
}
