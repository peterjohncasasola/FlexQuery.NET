using System.Text.RegularExpressions;

namespace FlexQuery.NET.Dapper.Sql.Builders;

internal static class SqlCountBuilder
{
    public static string ExtractCountSql(string sql)
    {
        var patterns = new[] { @"\bORDER\s+BY\b", @"\bLIMIT\b", @"\bOFFSET\b" };
        var minIdx = sql.Length;

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
            while (match.Success)
            {
                if (!  IsInsideParentheses(sql, match.Index))
                {
                    if (match.Index < minIdx)
                        minIdx = match.Index;
                    break;
                }
                match = match.NextMatch();
            }
        }

        var baseSql = sql[..minIdx];
        return $"SELECT COUNT(1) FROM ({baseSql.Trim()}) AS CountTable";
    }
    
    private static bool IsInsideParentheses(string sql, int index)
    {
        var depth = 0;
        for (var i = 0; i < index; i++)
        {
            switch (sql[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
            }
        }
        return depth > 0;
    }
}