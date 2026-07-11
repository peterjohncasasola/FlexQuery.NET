namespace FlexQuery.NET.OpenApi.Documentation;

internal static class SchemaDocumentation
{
    internal static readonly string FlexQueryRequest =
        "A structured query request supporting filtering, sorting, pagination, " +
        "field selection, and aggregation.";

    internal static readonly string FlexQueryParameters =
        "Query string parameters for filtering, sorting, pagination, " +
        "field selection, and aggregation.";

    internal static readonly string QueryResult =
        "A paginated query result with metadata including page information, " +
        "total counts, and aggregate data.";

    internal static readonly string FilterGroup =
        "A tree of filter conditions combined with AND/OR logic.";

    internal static readonly string FilterCondition =
        "A single field-level filter predicate with field, operator, and value.";

    internal static readonly string SortNode =
        "A sort specification with field name and direction.";

    internal static readonly string PagingOptions =
        "Pagination parameters: page number, page size, and disable option.";

    internal static readonly string AggregateModel =
        "An aggregate projection expression with function, field, and alias.";

    internal static readonly string HavingCondition =
        "A HAVING condition against an aggregate projection.";

    internal static readonly string IncludeNode =
        "A filtered navigation include path with optional child expansions.";

    internal static readonly string ProjectionMode =
        "Defines how projected data is shaped: Nested, Flat, or FlatMixed.";

    internal static readonly string LogicOperator =
        "Logical operator for combining filter conditions: And or Or.";

    internal static readonly string AggregateFunction =
        "Aggregate function: Sum, Count, Avg, Min, or Max.";
}
