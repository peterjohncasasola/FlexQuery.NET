using System.Linq.Expressions;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Security;

internal static class OperatorRegistry
{
    public static readonly IReadOnlyDictionary<string, Func<Expression, Expression, Expression>> BinaryFactories =
        new Dictionary<string, Func<Expression, Expression, Expression>>(StringComparer.OrdinalIgnoreCase)
        {
            [FilterOperators.Equal] = Expression.Equal,
            [FilterOperators.NotEqual] = Expression.NotEqual,
            [FilterOperators.GreaterThan] = Expression.GreaterThan,
            [FilterOperators.GreaterThanOrEq] = Expression.GreaterThanOrEqual,
            [FilterOperators.LessThan] = Expression.LessThan,
            [FilterOperators.LessThanOrEq] = Expression.LessThanOrEqual
        };
}
