namespace FlexQuery.NET.Helpers;

internal static class ReservedKeywordHelper
{
    private static readonly HashSet<string> ReservedKeywords = new(
    [
        // Query Clauses
        "SELECT",
        "FROM",
        "WHERE",
        "GROUP",
        "BY",
        "HAVING",
        "ORDER",
        "DISTINCT",
        "LIMIT",
        "OFFSET",
        "SKIP",
        "TAKE",
        "TOP",

        // Aliasing
        "AS",

        // Sorting
        "ASC",
        "DESC",

        // Logical Operators
        "AND",
        "OR",
        "NOT",

        // Comparison Operators
        "EQ",
        "NE",
        "GT",
        "GTE",
        "LT",
        "LTE",
        "IN",
        "NOTIN",
        "LIKE",
        "ILIKE",
        "BETWEEN",
        "IS",
        "NULL",
        "EXISTS",
        "ANY",
        "ALL",

        // Literals
        "TRUE",
        "FALSE",
        
        // String Functions
        "CONTAINS",
        "STARTSWITH",
        "ENDSWITH",
        "LENGTH",
        "LEN",
        "LOWER",
        "UPPER",
        "TRIM",
        "LTRIM",
        "RTRIM",
        "SUBSTRING",
        "REPLACE",
        "CONCAT",
        "AGGREGATE",

        // Numeric Functions
        "ABS",
        "ROUND",
        "CEILING",
        "FLOOR",

        // Null Functions
        "COALESCE",
        "ISNULL",

        // Date Functions
        "NOW",
        "TODAY",
        "DATE",
        "YEAR",
        "MONTH",
        "DAY",
        "HOUR",
        "MINUTE",
        "SECOND",

        // Conditional Expressions (Future)
        "CASE",
        "WHEN",
        "THEN",
        "ELSE",
        "END",

        // Join Keywords (Future)
        "JOIN",
        "INNER",
        "LEFT",
        "RIGHT",
        "FULL",
        "OUTER",
        "ON",

        // Set Operations (Future)
        "UNION",
        "INTERSECT",
        "EXCEPT",

        // Windowing (Future)
        "WITH",
        "OVER",
        "PARTITION",
        "WINDOW",
        "INTO"
    ], StringComparer.OrdinalIgnoreCase);
    
    public static bool IsReserved(string identifier)
        => ReservedKeywords.Contains(identifier);
}