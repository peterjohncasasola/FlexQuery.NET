using System.Linq.Expressions;

namespace FlexQuery.NET.Operators;

/// <summary>
/// Pluggable expression builder for a specific filter operator.
/// </summary>
public interface IOperatorHandler
{
    /// <summary>Canonical operator name this handler supports.</summary>
    string Operator { get; }

    /// <summary>Builds an expression for the supplied member and raw DSL value.</summary>
    /// <param name="member">The expression representing the field member.</param>
    /// <param name="rawValue">The raw string value from the filter DSL.</param>
    /// <returns>A LINQ expression representing the filter condition, or null if the operator cannot be applied.</returns>
    Expression? Build(Expression member, string? rawValue);
}

