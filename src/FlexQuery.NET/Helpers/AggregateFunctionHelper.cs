namespace FlexQuery.NET.Helpers;

internal static class AggregateFunctionHelper
{
    private static readonly HashSet<string> SupportedFunctions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "sum",
            "count",
            "avg",
            "min",
            "max"
        };

    public static bool IsSupported(string value) => SupportedFunctions.Contains(value);
}