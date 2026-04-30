using DynamicQueryable.Constants;
using DynamicQueryable.Extensions;
using DynamicQueryable.Extensions.EFCore;
using DynamicQueryable.Models;
using DynamicQueryable.Operators;
using DynamicQueryable.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DynamicQueryable.Tests.Tests;

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
