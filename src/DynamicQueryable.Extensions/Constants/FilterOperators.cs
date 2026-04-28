namespace DynamicQueryable.Constants;

/// <summary>
/// Canonical filter operator string constants.
/// All parsers must normalize operator strings to these values.
/// </summary>
public static class FilterOperators
{
    public const string Equal            = "eq";
    public const string NotEqual         = "neq";
    public const string GreaterThan      = "gt";
    public const string GreaterThanOrEq  = "gte";
    public const string LessThan         = "lt";
    public const string LessThanOrEq     = "lte";
    public const string Contains         = "contains";
    public const string StartsWith       = "startswith";
    public const string EndsWith         = "endswith";
    public const string IsNull           = "isnull";
    public const string IsNotNull        = "isnotnull";
    public const string In               = "in";

    /// <summary>Normalizes common variants to canonical operator strings.</summary>
    public static string Normalize(string? raw)
    {
        return (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "eq"           or "equal"    or "equals"           or "=="  => Equal,
            "neq"          or "ne"       or "notequal"         or "!="  => NotEqual,
            "gt"           or "greaterthan"                             => GreaterThan,
            "gte"          or "ge"       or "greaterthanorequal"        => GreaterThanOrEq,
            "lt"           or "lessthan"                                => LessThan,
            "lte"          or "le"       or "lessthanorequal"           => LessThanOrEq,
            "contains"     or "like"     or "cn"                        => Contains,
            "startswith"   or "starts"   or "sw"                        => StartsWith,
            "endswith"     or "ends"     or "ew"                        => EndsWith,
            "isnull"       or "null"                                    => IsNull,
            "isnotnull"    or "notnull"  or "isnotempty"                => IsNotNull,
            "in"                                                        => In,
            _                                                           => Equal
        };
    }
}
