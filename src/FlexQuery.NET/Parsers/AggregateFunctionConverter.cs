using System.ComponentModel;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Parsers;

internal static class AggregateFunctionConverter
{
    public static AggregateFunction Parse(string function) => function.ToLowerInvariant() switch
    {
        "sum" => AggregateFunction.Sum,
        "count" => AggregateFunction.Count,
        "avg" or "average" => AggregateFunction.Avg,
        "min" => AggregateFunction.Min,
        "max" => AggregateFunction.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(function), function, $"Unknown aggregate function '{function}'")
    };

    public static string ToKeyword(this AggregateFunction function)
    {
        if (!Enum.IsDefined(function))
        {
            throw new InvalidEnumArgumentException(
                nameof(function),
                (int)function,
                typeof(AggregateFunction));
        }

        return function.ToString().ToLowerInvariant();
    }

    
}
