using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET.Resolvers;

/// <summary>
/// Validates that a lambda selector represents a direct property access.
/// Rejects nested members, method calls, computed expressions, and anonymous types.
/// </summary>
internal static class DirectPropertySelector
{
    /// <summary>
    /// Extracts the <see cref="PropertyInfo"/> from a direct property-access lambda.
    /// </summary>
    /// <typeparam name="T">The lambda parameter type.</typeparam>
    /// <typeparam name="TProperty">The lambda return type.</typeparam>
    /// <param name="selector">The lambda expression to inspect.</param>
    /// <param name="parameterName">A human-readable name for the parameter, used in error messages.</param>
    /// <returns>The <see cref="PropertyInfo"/> for the accessed property.</returns>
    /// <exception cref="FlexQueryException">Thrown when the selector is not a direct property access.</exception>
    public static PropertyInfo Extract<T, TProperty>(
        Expression<Func<T, TProperty>> selector,
        string parameterName)
    {
        var body = UnwrapConversion(selector.Body);

        if (body is not MemberExpression { Member: PropertyInfo prop, Expression: ParameterExpression })
        {
            throw new FlexQueryException(
                $"{parameterName} must be a direct property access (e.g., x => x.Property). " +
                $"Received: {selector.Body}. Computed expressions are not supported in v1.");
        }

        return prop;
    }

    /// <summary>
    /// Attempts to extract the <see cref="PropertyInfo"/> from a direct property-access
    /// lambda without throwing.
    /// </summary>
    public static bool TryExtract<T, TProperty>(
        Expression<Func<T, TProperty>> selector,
        out PropertyInfo? property,
        string parameterName)
    {
        var body = UnwrapConversion(selector.Body);

        if (body is MemberExpression { Member: PropertyInfo prop, Expression: ParameterExpression })
        {
            property = prop;
            return true;
        }

        property = null;
        return false;
    }

    private static Expression UnwrapConversion(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }
        return expression;
    }
}
