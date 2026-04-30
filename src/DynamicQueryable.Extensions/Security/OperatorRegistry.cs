using System.Linq.Expressions;
using DynamicQueryable.Constants;

namespace DynamicQueryable.Security;

internal static class OperatorRegistry
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        FilterOperators.Equal,
        FilterOperators.NotEqual,
        FilterOperators.GreaterThan,
        FilterOperators.GreaterThanOrEq,
        FilterOperators.LessThan,
        FilterOperators.LessThanOrEq,
        FilterOperators.Contains,
        FilterOperators.StartsWith,
        FilterOperators.EndsWith,
        FilterOperators.Like,
        FilterOperators.In,
        FilterOperators.NotIn,
        FilterOperators.Between,
        FilterOperators.IsNull,
        FilterOperators.IsNotNull,
        FilterOperators.Any,
        FilterOperators.Count
    };

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

    public static bool IsAllowed(string op)
        => Allowed.Contains(op);
}
