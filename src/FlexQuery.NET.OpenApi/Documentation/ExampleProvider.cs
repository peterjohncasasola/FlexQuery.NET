using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.OpenApi.Documentation;

internal static class ExampleProvider
{
    internal static FlexQueryRequest CreateRequestExample() => new()
    {
        Filter = new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            [
                new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" },
                new FilterCondition { Field = "Age", Operator = "gte", Value = "18" }
            ],
            Groups =
            [
                new FilterGroup
                {
                    Logic = LogicOperator.Or,
                    Filters =
                    [
                        new FilterCondition { Field = "Role", Operator = "eq", Value = "Admin" },
                        new FilterCondition { Field = "Role", Operator = "eq", Value = "Manager" }
                    ]
                }
            ]
        },
        Sort =
        [
            new SortNode { Field = "LastName", Descending = false },
            new SortNode { Field = "FirstName", Descending = false }
        ],
        Select = ["Id", "FirstName", "LastName", "Email", "Profile.AvatarUrl"],
        Include = ["Orders", "Profile"],
        Expand =
        [
            new IncludeNode
            {
                Path = "Orders",
                Filter = new FilterGroup
                {
                    Filters = [new FilterCondition { Field = "Status", Operator = "neq", Value = "Cancelled" }]
                },
                Children =
                [
                    new IncludeNode { Path = "OrderItems" }
                ]
            }
        ],
        Aggregate =
        [
            new AggregateModel { Function = AggregateFunction.Sum, Field = "TotalAmount", Alias = "TotalRevenue" },
            new AggregateModel { Function = AggregateFunction.Count, Alias = "OrderCount" },
            new AggregateModel { Function = AggregateFunction.Avg, Field = "Rating", Alias = "AvgRating" }
        ],
        GroupBy = ["Region", "Category"],
        Having = new HavingCondition
        {
            Function = AggregateFunction.Sum,
            Field = "TotalAmount",
            Operator = "gt",
            Value = "1000"
        },
        Mode = ProjectionMode.FlatMixed,
        Distinct = true,
        Paging = new PagingOptions { Page = 1, PageSize = 20 },
        IncludeCount = true
    };

    internal static FlexQueryParameters CreateParametersExample() => new()
    {
        Filter = "Status:eq:Active,Age:gte:18&(Role:eq:Admin|Role:eq:Manager)",
        Sort = "LastName:asc,FirstName:asc",
        Select = "Id,FirstName,LastName,Email,Profile.AvatarUrl",
        Include = "Orders,Profile",
        Page = 1,
        PageSize = 20,
        IncludeCount = true,
        Distinct = true,
        GroupBy = "Region,Category",
        Aggregate = "SUM(TotalAmount):TotalRevenue,COUNT:OrderCount,AVG(Rating):AvgRating",
        Having = "SUM(TotalAmount):gt:1000",
        Mode = "FlatMixed"
    };

    internal static QueryResult<Dictionary<string, object>> CreateQueryResultExample() => new()
    {
        Data =
        [
            new Dictionary<string, object>
            {
                ["id"] = 1,
                ["firstName"] = "John",
                ["lastName"] = "Doe",
                ["email"] = "john.doe@example.com",
                ["status"] = "Active"
            },
            new Dictionary<string, object>
            {
                ["id"] = 2,
                ["firstName"] = "Jane",
                ["lastName"] = "Smith",
                ["email"] = "jane.smith@example.com",
                ["status"] = "Active"
            }
        ],
        TotalCount = 125,
        ResultCount = 125,
        Page = 1,
        PageSize = 20,
        Aggregates = new Dictionary<string, Dictionary<string, object>>
        {
            ["North"] = new() { ["TotalRevenue"] = 45200m, ["OrderCount"] = 312, ["AvgRating"] = 4.2d },
            ["South"] = new() { ["TotalRevenue"] = 31800m, ["OrderCount"] = 198, ["AvgRating"] = 3.9d }
        },
        NextCursorToken = "eyJsYXN0X3ZhbHVlIjoxMjV9"
    };
}
