using FlexQuery.NET.Models;

namespace FlexQuery.NET.EntityFrameworkCore.SqlFormatting;

internal static class SqlParameterExtractor
{
    private const string ParamSetPrefix = ".param set ";

    public static (string Sql, IReadOnlyList<QueryParameter> Parameters) Extract(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return (sql ?? string.Empty, []);

        var lines = sql.Split('\n');
        var cleanLines = new List<string>();
        var parameters = new List<QueryParameter>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith(ParamSetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var argument = trimmed[ParamSetPrefix.Length..].Trim();
                var spaceIndex = argument.IndexOf(' ');

                if (spaceIndex > 0)
                {
                    var name = argument[..spaceIndex];
                    var valueString = argument[(spaceIndex + 1)..];
                    parameters.Add(new QueryParameter(name, TryParseValue(valueString)));
                    continue;
                }

                parameters.Add(new QueryParameter(argument, null));
                continue;
            }

            cleanLines.Add(line);
        }

        return (string.Join("\n", cleanLines), parameters);
    }

    private static object? TryParseValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Length > 1 && value[0] == '\'' && value[^1] == '\'')
            return value[1..^1];

        if (int.TryParse(value, out var i)) return i;
        if (long.TryParse(value, out var l)) return l;
        if (double.TryParse(value, out var d)) return d;
        if (bool.TryParse(value, out var b)) return b;
        if (DateTime.TryParse(value, out var dt)) return dt;

        return value;
    }
}
