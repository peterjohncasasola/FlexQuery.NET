using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Integration;

public class GroupedQueryDapperTests
{
    private readonly MappingRegistry _registry = CreateRegistry();

    private static MappingRegistry CreateRegistry()
    {
        var registry = new MappingRegistry();

        registry.Entity<Order>()
            .ToTable("Orders")
            .HasMany(o => o.OrderItems).HasForeignKey("OrderId");

        registry.Entity<OrderItem>().ToTable("OrderItems");

        return registry;
    }

    private SqlCommand Translate(QueryOptions options)
    {
        options.Items[ContextKeys.EntityType] = typeof(Order);

        return new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
    }

    private static QueryOptions BaseGroupedOptions()
    {
        return new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],
            Paging = { Disabled = true }
        };
    }

    [Fact]
    public void Dapper_GroupedQuery_InvalidDetailSort_ThrowsValidationError()
    {
        var options = BaseGroupedOptions();
        options.Sort = [new SortNode { Field = "Id" }];

        Action act = () => Translate(options);

        act.Should().Throw<QueryValidationException>()
            .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GroupBySortInvalid);
    }

    [Fact]
    public void Dapper_GroupedQuery_AllInvalidSorts_ThrowsValidationError()
    {
        var options = BaseGroupedOptions();
        options.Sort = [
            new SortNode { Field = "Id" },
            new SortNode { Field = "Number" }
        ];

        Action act = () => Translate(options);

        act.Should().Throw<QueryValidationException>()
            .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GroupBySortInvalid);
    }

    [Fact]
    public void Dapper_GroupedQuery_AggregateAliasSort_GeneratesValidOrderBy()
    {
        var options = BaseGroupedOptions();
        options.Sort = [new SortNode { Field = "totalSum", Descending = true }];

        var command = Translate(options);

        command.Sql.Should().Contain("ORDER BY \"totalSum\" DESC");
    }

    [Fact]
    public void Dapper_GroupedQuery_AggregateFieldSort_ResolvesToAlias()
    {
        var options = BaseGroupedOptions();
        options.Sort = [new SortNode { Field = "Total", Descending = true }];

        var command = Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("\"totalSum\" DESC");
    }

    [Fact]
    public void Dapper_GroupedQuery_GroupKeySort_GeneratesValidOrderBy()
    {
        var options = BaseGroupedOptions();
        options.Sort = [new SortNode { Field = "CustomerId", Descending = true }];

        var command = Translate(options);

        command.Sql.Should().Contain("ORDER BY \"CustomerId\" DESC");
        command.Sql.Should().Contain("GROUP BY \"CustomerId\"");
    }

    [Fact]
    public void Dapper_GroupedQuery_PagingWithoutSort_DefaultsToGroupKeyOrder()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],
            Paging = { Page = 2, PageSize = 10 }
        };

        var command = Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("\"CustomerId\"");
        command.Sql.Should().Contain("LIMIT");
        command.Sql.Should().Contain("OFFSET");
    }
}
