using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET;

internal static class LogicalOperatorExtensions
{
    public static string ToKeyword(this LogicOperator logicOperator) => logicOperator switch
    {
        LogicOperator.And => "AND",
        LogicOperator.Or => "OR",
        _ => throw new ArgumentOutOfRangeException(nameof(logicOperator), logicOperator, null)
    };
}