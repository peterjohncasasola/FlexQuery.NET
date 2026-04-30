using System.Linq.Expressions;

namespace DynamicQueryable.Operators;

/// <summary>
/// Pluggable expression builder for a specific filter operator.
/// </summary>
public interface IOperatorHandler
{
    /// <summary>Canonical operator name this handler supports.</summary>
    string Operator { get; }

    /// <summary>Builds an expression for the supplied member and raw DSL value.</summary>
    Expression? Build(Expression member, string? rawValue);
}
