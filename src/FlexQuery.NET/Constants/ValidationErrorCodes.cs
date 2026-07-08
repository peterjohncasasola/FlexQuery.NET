namespace FlexQuery.NET.Constants;

/// <summary>
/// Standard error codes used in validation rules.
/// </summary>
internal static class ValidationErrorCodes
{
    public const string FieldNotFound = "FIELD_NOT_FOUND";
    public const string TypeMismatch = "TYPE_MISMATCH";
    public const string FieldAccessDenied = "FIELD_ACCESS_DENIED";
    public const string IncludeAccessDenied = "INCLUDE_ACCESS_DENIED";
    public const string InvalidOperator = "INVALID_OPERATOR";
    public const string OperatorNotAllowed = "OPERATOR_NOT_ALLOWED";
    public const string NotACollection = "NOT_A_COLLECTION";
    public const string HavingWithoutGroupBy = "HAVING_WITHOUT_GROUPBY";
    public const string HavingAliasMismatch = "HAVING_ALIAS_MISMATCH";
    public const string GroupByIncludeConflict = "GROUPBY_INCLUDE_CONFLICT";
    public const string IncludePathNotFound = "INCLUDE_PATH_NOT_FOUND";
    public const string CursorMismatch = "CURSOR_MISMATCH";
    public const string CursorNullValue = "CURSOR_NULL_VALUE";
    public const string PaginationModeConflict = "PAGINATION_MODE_CONFLICT";
}
