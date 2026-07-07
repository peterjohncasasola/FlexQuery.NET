using System.Linq.Expressions;

namespace FlexQuery.NET.Helpers;

internal sealed class ParameterRebinder : ExpressionVisitor
{
    private readonly Dictionary<ParameterExpression, Expression> _map;

    private ParameterRebinder(Dictionary<ParameterExpression, Expression>? map)
    {
        _map = map ?? new Dictionary<ParameterExpression, Expression>();
    }

    public static Expression ReplaceParameters(Dictionary<ParameterExpression, Expression> map, Expression exp)
    {
        return new ParameterRebinder(map).Visit(exp);
    }

    protected override Expression VisitParameter(ParameterExpression p)
    {
        if (_map.TryGetValue(p, out var replacement))
        {
            return replacement;
        }
        return base.VisitParameter(p);
    }
}
