namespace FlexQuery.NET.Constants;

/// <summary>
/// Canonical filter operator string constants.
/// All parsers must normalize operator strings to these values.
/// </summary>
public static class FilterOperators
{
    /// <summary>Equality operator.</summary>
    public const string Equal = "eq";
    /// <summary>Inequality operator.</summary>
    public const string NotEqual = "neq";
    /// <summary>Greater than operator.</summary>
    public const string GreaterThan = "gt";
    /// <summary>Greater than or equal operator.</summary>
    public const string GreaterThanOrEq = "gte";
    /// <summary>Less than operator.</summary>
    public const string LessThan = "lt";
    /// <summary>Less than or equal operator.</summary>
    public const string LessThanOrEq = "lte";
    /// <summary>Substring search operator.</summary>
    public const string Contains = "contains";
    /// <summary>Prefix search operator.</summary>
    public const string StartsWith = "startswith";
    /// <summary>Suffix search operator.</summary>
    public const string EndsWith = "endswith";
    /// <summary>Null check operator.</summary>
    public const string IsNull = "isnull";
    /// <summary>Not null check operator.</summary>
    public const string IsNotNull = "isnotnull";
    /// <summary>Collection containment operator.</summary>
    public const string In = "in";
    /// <summary>Collection exclusion operator.</summary>
    public const string NotIn = "notin";
    /// <summary>Inclusive range operator.</summary>
    public const string Between = "between";
    /// <summary>SQL LIKE pattern operator.</summary>
    public const string Like = "like";
    /// <summary>Collection any operator.</summary>
    public const string Any = "any";
    /// <summary>Collection all operator.</summary>
    public const string All = "all";
    /// <summary>Collection count operator.</summary>
    public const string Count = "count";

    /// <summary>
    /// Maps every recognized alias (including each canonical value itself) to its
    /// canonical operator string. Lookup is case-insensitive.
    /// </summary>
    private static readonly Dictionary<string, string> AliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = Equal, ["equal"] = Equal, ["equals"] = Equal, ["=="] = Equal, ["="] = Equal,

        ["neq"] = NotEqual, ["ne"] = NotEqual, ["notequal"] = NotEqual, ["!="] = NotEqual,

        ["gt"] = GreaterThan, ["greaterthan"] = GreaterThan, [">"] = GreaterThan,

        ["gte"] = GreaterThanOrEq, ["ge"] = GreaterThanOrEq,
        ["greaterthanorequal"] = GreaterThanOrEq, [">="] = GreaterThanOrEq,

        ["lt"] = LessThan, ["lessthan"] = LessThan, ["<"] = LessThan,

        ["lte"] = LessThanOrEq, ["le"] = LessThanOrEq,
        ["lessthanorequal"] = LessThanOrEq, ["<="] = LessThanOrEq,

        ["contains"] = Contains, ["cn"] = Contains,

        ["like"] = Like,

        ["startswith"] = StartsWith, ["starts"] = StartsWith, ["sw"] = StartsWith,

        ["endswith"] = EndsWith, ["ends"] = EndsWith, ["ew"] = EndsWith,

        ["isnull"] = IsNull, ["null"] = IsNull,

        ["isnotnull"] = IsNotNull, ["notnull"] = IsNotNull, ["isnotempty"] = IsNotNull,

        ["in"] = In,

        ["notin"] = NotIn, ["not in"] = NotIn,

        ["between"] = Between,

        ["any"] = Any,

        ["all"] = All,

        ["count"] = Count,
    };

    /// <summary>Normalizes common variants to canonical operator strings.</summary>
    /// <param name="raw">The raw operator string as provided by a parser or caller. May be <c>null</c>.</param>
    /// <returns>
    /// The canonical operator constant (e.g. <see cref="Equal"/>) if <paramref name="raw"/> matches a
    /// known alias; otherwise the trimmed, lowercased form of <paramref name="raw"/> unchanged.
    /// </returns>
    public static string Normalize(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();

        return AliasMap.TryGetValue(trimmed, out var canonical)
            ? canonical
            : trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Gets the set of all canonical operators supported by <see cref="IsSupported"/>.
    /// Lookup is case-insensitive.
    /// </summary>
    private static IReadOnlySet<string> SupportedOperators { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Equal, NotEqual, GreaterThan, GreaterThanOrEq, LessThan, LessThanOrEq,
        Contains, StartsWith, EndsWith, IsNull, IsNotNull, In, NotIn, Between, Like,
        Any, All, Count
    };

    /// <summary>
    /// Checks if the provided operator string is supported, after normalizing aliases
    /// and casing via <see cref="Normalize"/>.
    /// </summary>
    /// <param name="op">The operator string to check. May be <c>null</c> or whitespace.</param>
    /// <returns><c>true</c> if <paramref name="op"/> normalizes to a known canonical operator; otherwise <c>false</c>.</returns>
    public static bool IsSupported(string? op)
    {
        if (string.IsNullOrWhiteSpace(op)) return false;
        return SupportedOperators.Contains(Normalize(op));
    }

    /// <summary>
    /// Determines whether the given operator string represents a collection operator
    /// (<see cref="Any"/>, <see cref="All"/>, or <see cref="Count"/>) once normalized.
    /// </summary>
    /// <param name="op">The operator string to check.</param>
    /// <returns><c>true</c> if <paramref name="op"/> normalizes to <see cref="Any"/>, <see cref="All"/>, or <see cref="Count"/>; otherwise <c>false</c>.</returns>
    public static bool IsCollectionOperator(string op)
    {
        var normalized= Normalize(op);

        return normalized is Any
            or All
            or Count;
    }

    /// <summary>
    /// Determines whether the given operator string represents a null-check operator
    /// (<see cref="IsNull"/> or <see cref="IsNotNull"/>) once normalized.
    /// </summary>
    /// <param name="op">The operator string to check.</param>
    /// <returns><c>true</c> if <paramref name="op"/> normalizes to <see cref="IsNull"/> or <see cref="IsNotNull"/>; otherwise <c>false</c>.</returns>
    public static bool IsNullOperator(string op)
    {
        var normalized= Normalize(op);

        return normalized is IsNull
            or IsNotNull;
    }
}