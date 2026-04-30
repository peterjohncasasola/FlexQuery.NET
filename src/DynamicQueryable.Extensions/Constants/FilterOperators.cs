namespace DynamicQueryable.Constants;

/// <summary>
/// Canonical filter operator string constants.
/// All parsers must normalize operator strings to these values.
/// </summary>
public static class FilterOperators
{
    /// <summary>Equality operator.</summary>
    public const string Equal            = "eq";
    /// <summary>Inequality operator.</summary>
    public const string NotEqual         = "neq";
    /// <summary>Greater than operator.</summary>
    public const string GreaterThan      = "gt";
    /// <summary>Greater than or equal operator.</summary>
    public const string GreaterThanOrEq  = "gte";
    /// <summary>Less than operator.</summary>
    public const string LessThan         = "lt";
    /// <summary>Less than or equal operator.</summary>
    public const string LessThanOrEq     = "lte";
    /// <summary>Substring search operator.</summary>
    public const string Contains         = "contains";
    /// <summary>Prefix search operator.</summary>
    public const string StartsWith       = "startswith";
    /// <summary>Suffix search operator.</summary>
    public const string EndsWith         = "endswith";
    /// <summary>Null check operator.</summary>
    public const string IsNull           = "isnull";
    /// <summary>Not null check operator.</summary>
    public const string IsNotNull        = "isnotnull";
    /// <summary>Collection containment operator.</summary>
    public const string In               = "in";
    /// <summary>Collection exclusion operator.</summary>
    public const string NotIn            = "notin";
    /// <summary>Inclusive range operator.</summary>
    public const string Between          = "between";
    /// <summary>SQL LIKE pattern operator.</summary>
    public const string Like             = "like";
    /// <summary>Collection any operator.</summary>
    public const string Any              = "any";
    /// <summary>Collection all operator.</summary>
    public const string All              = "all";
    /// <summary>Collection count operator.</summary>
    public const string Count            = "count";

    /// <summary>Normalizes common variants to canonical operator strings.</summary>
    public static string Normalize(string? raw)
    {
        return (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "eq"           or "equal"    or "equals"           or "=="  or "="   => Equal,
            "neq"          or "ne"       or "notequal"         or "!="  => NotEqual,
            "gt"           or "greaterthan"                   or ">"     => GreaterThan,
            "gte"          or "ge"       or "greaterthanorequal" or ">=" => GreaterThanOrEq,
            "lt"           or "lessthan"                      or "<"     => LessThan,
            "lte"          or "le"       or "lessthanorequal" or "<="    => LessThanOrEq,
            "contains"     or "cn"                                      => Contains,
            "like"                                                       => Like,
            "startswith"   or "starts"   or "sw"                        => StartsWith,
            "endswith"     or "ends"     or "ew"                        => EndsWith,
            "isnull"       or "null"                                    => IsNull,
            "isnotnull"    or "notnull"  or "isnotempty"                => IsNotNull,
            "in"                                                        => In,
            "notin"        or "not in"                                  => NotIn,
            "between"                                                   => Between,
            "any"                                                       => Any,
            "all"                                                       => All,
            "count"                                                     => Count,
            _                                                           => (raw ?? string.Empty).Trim().ToLowerInvariant()
        };
    }
}
