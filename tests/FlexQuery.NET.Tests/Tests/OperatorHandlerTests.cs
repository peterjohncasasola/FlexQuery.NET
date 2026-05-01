using FlexQuery.NET.Constants;
using FlexQuery.NET;
using FlexQuery.NET.EFCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Operators;
using FlexQuery.NET.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Tests;

public class OperatorHandlerTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Core_DefaultLikeHandler_UsesFallbackInsteadOfEfFunctionsLike()
    {
        OperatorHandlerRegistry.ResetToDefaults();

        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition
                {
                    Field = "Name",
                    Operator = FilterOperators.Like,
                    Value = "%Bo%"
                }]
            },
            Paging = { Disabled = true }
        };

        var sql = _db.Customers.AsQueryable().ApplyFilter(opts).ToQueryString();

        sql.ToUpperInvariant().Should().NotContain("LIKE");
    }

    [Fact]
    public void EfCore_UseEfCoreOperators_OverridesLikeHandlerToEfFunctionsLike()
    {
        OperatorHandlerRegistry.ResetToDefaults();

        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition
                {
                    Field = "Name",
                    Operator = FilterOperators.Like,
                    Value = "%Bo%"
                }]
            },
            Paging = { Disabled = true }
        }.UseEfCoreOperators();

        var sql = _db.Customers.AsQueryable().ApplyFilter(opts).ToQueryString();

        sql.ToUpperInvariant().Should().Contain("LIKE");
    }
}
