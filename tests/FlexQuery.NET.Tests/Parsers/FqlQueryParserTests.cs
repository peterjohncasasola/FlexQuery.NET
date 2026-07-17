using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.NET.Tests.Parsers;

public class FqlQueryParserTests
{
    private static FilterGroup FqlParseFilter(string Fql) =>
        new FqlQueryParser().Parse(Fql);

    private static QueryOptions FqlParse(FlexQueryParameters parameters) =>
        new FqlQueryParser().Parse(parameters);

    private static QueryOptions FqlParse(string? filter = null, string? sort = null,
        string? groupBy = null, string? select = null, string? having = null,
        string? aggregates = null, string? include = null, bool? distinct = null,
        int? page = null, int? pageSize = null)
    {
        return FqlParse(new FlexQueryParameters
        {
            Filter = filter,
            Sort = sort,
            GroupBy = groupBy,
            Select = select,
            Having = having,
            Aggregate = aggregates,
            Include = include,
            Distinct = distinct,
            Page = page,
            PageSize = pageSize
        });
    }

    [Fact]
    public void Fql_SimpleCondition_ParsedCorrectly()
    {
        var filter = FqlParseFilter("name = \"john\"");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Fql_AndCondition_ParsedCorrectly()
    {
        var filter = FqlParseFilter("name = \"john\" AND age >= 20");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().HaveCount(2);
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Operator == FilterOperators.Equal && f.Value == "john");
        filter.Filters.Should().Contain(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThanOrEq && f.Value == "20");
    }

    [Fact]
    public void Fql_OrCondition_ParsedCorrectly()
    {
        var filter = FqlParseFilter("name = \"john\" OR name = \"doe\"");

        filter.Logic.Should().Be(LogicOperator.Or);
        filter.Filters.Should().HaveCount(2);
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Value == "john");
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Value == "doe");
    }

    [Fact]
    public void Fql_NestedParentheses_ParsedCorrectly()
    {
        var filter = FqlParseFilter("(name = \"john\" OR name = \"doe\") AND age > 18");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().ContainSingle(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThan && f.Value == "18");
        filter.Groups.Should().ContainSingle();
        filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        filter.Groups[0].Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Fql_InOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("status IN (\"active\",\"pending\")");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("status");
        filter.Filters[0].Operator.Should().Be(FilterOperators.In);
        filter.Filters[0].Value.Should().Be("active,pending");
    }

    [Fact]
    public void Fql_NestedPropertyPath_ParsedCorrectly()
    {
        var filter = FqlParseFilter("orders.customer.name CONTAINS \"john\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders.customer.name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Fql_EmailAndNestedNumericCondition_ParsedCorrectly()
    {
        var filter = FqlParseFilter("email = \"ops@acmeretail.com\" AND orders.number = \"ORD-2026-0002\" AND orders.items.quantity > 2");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().HaveCount(3);
        filter.Filters.Should().Contain(f => f.Field == "email" && f.Operator == FilterOperators.Equal && f.Value == "ops@acmeretail.com");
        filter.Filters.Should().Contain(f => f.Field == "orders.number" && f.Operator == FilterOperators.Equal && f.Value == "ORD-2026-0002");
        filter.Filters.Should().Contain(f => f.Field == "orders.items.quantity" && f.Operator == FilterOperators.GreaterThan && f.Value == "2");
    }

    [Fact]
    public void Fql_BetweenOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("age BETWEEN 18 AND 60");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("age");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Between);
        filter.Filters[0].Value.Should().Be("18,60");
    }

    [Fact]
    public void Fql_IsNullOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("deletedAt IS NULL");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("deletedAt");
        filter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
        filter.Filters[0].Value.Should().BeNull();
    }

    [Fact]
    public void Fql_IsNotNullOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("deletedAt IS NOT NULL");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("deletedAt");
        filter.Filters[0].Operator.Should().Be(FilterOperators.IsNotNull);
        filter.Filters[0].Value.Should().BeNull();
    }

    [Fact]
    public void Fql_StartsWithOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("name STARTSWITH \"admin\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.StartsWith);
        filter.Filters[0].Value.Should().Be("admin");
    }

    [Fact]
    public void Fql_EndsWithOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("email ENDSWITH \".com\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("email");
        filter.Filters[0].Operator.Should().Be(FilterOperators.EndsWith);
        filter.Filters[0].Value.Should().Be(".com");
    }

    [Fact]
    public void Fql_LikeOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("name LIKE \"%john%\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Like);
        filter.Filters[0].Value.Should().Be("%john%");
    }

    [Fact]
    public void Fql_AnyOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("orders ANY total > 1000");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Any);
        filter.Filters[0].Value.Should().Be("total:gt:1000");
    }

    [Fact]
    public void Fql_AllOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("orders ALL status = \"completed\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.All);
        filter.Filters[0].Value.Should().Be("status:eq:completed");
    }

    [Fact]
    public void Fql_CountOperator_ParsedCorrectly()
    {
        var filter = FqlParseFilter("orders COUNT > 5");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Count);
        filter.Filters[0].Value.Should().Be("gt:5");
    }

    [Fact]
    public void Fql_ScopedAny_DotSyntax_ParsedToScopedFilter()
    {
        var filter = FqlParseFilter("orders.any(status = \"Cancelled\")");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "status" && f.Value == "Cancelled");
    }

    [Fact]
    public void Fql_ScopedAll_DotSyntax_ParsedToScopedFilter()
    {
        var filter = FqlParseFilter("orders.all(status = \"Active\")");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.All);
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "status" && f.Value == "Active");
    }

    [Fact]
    public void Fql_ScopedBracket_ParsedAsAnyCollectionNode()
    {
        var filter = FqlParseFilter("orders[status = \"Cancelled\"]");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "status" && f.Value == "Cancelled");
    }

    [Fact]
    public void Fql_ScopedAny_MultipleConditions_ParsedAsAndLogicalInsideCollection()
    {
        var filter = FqlParseFilter("orders.any(status = \"Cancelled\" AND total > 500)");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.ScopedFilter.Should().NotBeNull();

        var inner = cond.ScopedFilter!;
        inner.Logic.Should().Be(LogicOperator.And);
        inner.Filters.Should().HaveCount(2);
        inner.Filters.Should().Contain(f => f.Field == "status" && f.Value == "Cancelled");
        inner.Filters.Should().Contain(f => f.Field == "total" && f.Operator == FilterOperators.GreaterThan);
    }

    [Fact]
    public void Fql_ScopedAny_WithOrInsideGroup_ParsedCorrectly()
    {
        var filter = FqlParseFilter("orders.any(status = \"Cancelled\" OR status = \"Refunded\")");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Logic.Should().Be(LogicOperator.Or);
        cond.ScopedFilter.Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Fql_NestedScopedCollections_ParsedRecursively()
    {
        var filter = FqlParseFilter("orders.any(status = \"Cancelled\" AND orderItems.any(id = 101))");

        filter.Filters.Should().ContainSingle();
        var outer = filter.Filters[0];
        outer.Field.Should().Be("orders");
        outer.Operator.Should().Be(FilterOperators.Any);
        outer.ScopedFilter.Should().NotBeNull();

        var innerGroup = outer.ScopedFilter!;
        innerGroup.Logic.Should().Be(LogicOperator.And);
        innerGroup.Filters.Should().HaveCount(2);

        innerGroup.Filters.Should().Contain(f => f.Field == "status").Subject.Value.Should().Be("Cancelled");

        var nested = innerGroup.Filters.Should().Contain(f => f.Field == "orderItems").Subject;
        nested.Operator.Should().Be(FilterOperators.Any);
        nested.ScopedFilter.Should().NotBeNull();
        nested.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "id" && f.Value == "101");
    }

    [Fact]
    public void Fql_ScopedAny_CombinedWithFlatCondition_ParsedToAndLogical()
    {
        var filter = FqlParseFilter("name = \"Alice\" AND orders.any(total > 1000)");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().HaveCount(2);
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Operator == FilterOperators.Equal && f.Value == "Alice");

        var coll = filter.Filters.Should().Contain(f => f.Field == "orders").Subject;
        coll.Operator.Should().Be(FilterOperators.Any);
        coll.ScopedFilter.Should().NotBeNull();
    }

    [Fact]
    public void Fql_ScopedAny_ConvertsToFilterConditionWithScopedFilter()
    {
        var filter = FqlParseFilter("orders.any(status = \"Cancelled\" AND total > 500)");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().ContainSingle();

        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.Value.Should().BeNull();
        cond.ScopedFilter.Should().NotBeNull();

        var inner = cond.ScopedFilter!;
        inner.Logic.Should().Be(LogicOperator.And);
        inner.Filters.Should().HaveCount(2);
        inner.Filters.Should().Contain(f => f.Field == "status" && f.Value == "Cancelled");
        inner.Filters.Should().Contain(f => f.Field == "total" && f.Operator == FilterOperators.GreaterThan);
    }

    [Fact]
    public void Fql_BracketSyntax_ConvertsToFilterConditionWithScopedFilter()
    {
        var filter = FqlParseFilter("orders[status = \"Cancelled\"]");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "status");
    }

    [Fact]
    public void Fql_NestedScopedCollections_ConvertsToNestedScopedFilters()
    {
        var filter = FqlParseFilter("orders.any(status = \"Cancelled\" AND orderItems.any(id = 101))");

        filter.Filters.Should().ContainSingle();
        var outer = filter.Filters[0];
        outer.Field.Should().Be("orders");
        outer.ScopedFilter.Should().NotBeNull();

        var innerGroup = outer.ScopedFilter!;
        innerGroup.Logic.Should().Be(LogicOperator.And);
        innerGroup.Filters.Should().HaveCount(2);

        var statusCond = innerGroup.Filters.Should()
            .Contain(f => f.Field == "status").Subject;
        statusCond.Value.Should().Be("Cancelled");

        var nestedCond = innerGroup.Filters.Should()
            .Contain(f => f.Field == "orderItems").Subject;
        nestedCond.ScopedFilter.Should().NotBeNull();
        nestedCond.ScopedFilter!.Filters.Should()
            .ContainSingle(f => f.Field == "id" && f.Value == "101");
    }

    [Fact]
    public void Fql_FlatConditions_StillWorkAfterScopedSupport()
    {
        var filter = FqlParseFilter("orders.customer.name CONTAINS \"john\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders.customer.name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        filter.Filters[0].Value.Should().Be("john");
        filter.Filters[0].ScopedFilter.Should().BeNull();
    }

    // â”€â”€â”€ Sort Parser Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FqlSort_SingleAsc_ParsedCorrectly()
    {
        var result = FqlParse(sort: "Name ASC");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void FqlSort_SingleDesc_ParsedCorrectly()
    {
        var result = FqlParse(sort: "CreatedDate DESC");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("CreatedDate");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void FqlSort_MultipleFields_ParsedCorrectly()
    {
        var result = FqlParse(sort: "Customer.Name DESC, CreatedDate ASC");

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("Customer.Name");
        result.Sort[0].Descending.Should().BeTrue();
        result.Sort[1].Field.Should().Be("CreatedDate");
        result.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void FqlSort_DefaultAsc_WhenNoDirectionSpecified()
    {
        var result = FqlParse(sort: "Name");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void FqlSort_NestedPropertyPath_ParsedCorrectly()
    {
        var result = FqlParse(sort: "Orders.Customer.Name ASC");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Orders.Customer.Name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void FqlSort_CaseInsensitiveDirection_ParsedCorrectly()
    {
        var result = FqlParse(sort: "Name desc, Age ASC");

        result.Sort[0].Descending.Should().BeTrue();
        result.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void FqlSort_EmptyString_ReturnsEmptyList()
    {
        var result = FqlParse(sort: "");

        result.Sort.Should().BeEmpty();
    }

    [Fact]
    public void FqlSort_NullString_ReturnsEmptyList()
    {
        var result = FqlParse();

        result.Sort.Should().BeEmpty();
    }

    [Fact]
    public void FqlSort_AggregateSum_Desc_ParsedCorrectly()
    {
        var result = FqlParse(sort: "SUM(Orders.Total) DESC");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Orders");
        result.Sort[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result.Sort[0].AggregateField.Should().Be("Total");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void FqlSort_AggregateSum_DefaultAsc_ParsedCorrectly()
    {
        var result = FqlParse(sort: "SUM(Orders.Total)");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Orders");
        result.Sort[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result.Sort[0].AggregateField.Should().Be("Total");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void FqlSort_AggregateCount_NoField_ParsedCorrectly()
    {
        var result = FqlParse(sort: "COUNT(Orders) ASC");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Orders");
        result.Sort[0].Aggregate.Should().Be(AggregateFunction.Count);
        result.Sort[0].AggregateField.Should().BeNull();
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void FqlSort_Aggregate_CaseInsensitive_ParsedCorrectly()
    {
        var result = FqlParse(sort: "sum(Orders.Total) desc, AVG(Orders.Price) ASC");

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("Orders");
        result.Sort[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result.Sort[0].AggregateField.Should().Be("Total");
        result.Sort[0].Descending.Should().BeTrue();
        result.Sort[1].Field.Should().Be("Orders");
        result.Sort[1].Aggregate.Should().Be(AggregateFunction.Avg);
        result.Sort[1].AggregateField.Should().Be("Price");
        result.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void FqlSort_Aggregate_InvalidDirection_Throws()
    {
        var act = () => FqlParse(sort: "SUM(Orders.Total) SIDEWAYS");

        act.Should().Throw<QueryParseException>();
    }

    [Fact]
    public void FqlSort_Aggregate_UnknownFunction_Throws()
    {
        var act = () => FqlParse(sort: "UNKNOWN(Orders.Total) DESC");

        act.Should().Throw<QueryParseException>();
    }

    [Fact]
    public void FqlSort_Aggregate_MissingField_Throws()
    {
        var act = () => FqlParse(sort: "SUM() DESC");

        act.Should().Throw<QueryParseException>();
    }

    // â”€â”€â”€ Aggregate Parser Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FqlAggregate_SumWithAlias_ParsedCorrectly()
    {
        var result = FqlParse(aggregates: "SUM(Amount) AS TotalSales");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Sum);
        result.Aggregates[0].Field.Should().Be("Amount");
        result.Aggregates[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void FqlAggregate_CountStar_ParsedCorrectly()
    {
        var result = FqlParse(aggregates: "COUNT(Orders)");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Count);
        result.Aggregates[0].Field.Should().Be("Orders");
        result.Aggregates[0].Alias.Should().Be("OrdersCount");
    }

    [Fact]
    public void FqlAggregate_MultipleAggregates_ParsedCorrectly()
    {
        var result = FqlParse(aggregates: "SUM(Amount), AVG(Price), COUNT(Orders) AS OrderCount");

        result.Aggregates.Should().HaveCount(3);
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Sum);
        result.Aggregates[0].Field.Should().Be("Amount");
        result.Aggregates[1].Function.Should().Be(AggregateFunction.Avg);
        result.Aggregates[1].Field.Should().Be("Price");
        result.Aggregates[2].Function.Should().Be(AggregateFunction.Count);
        result.Aggregates[2].Field.Should().Be("Orders");
        result.Aggregates[2].Alias.Should().Be("OrderCount");
    }

    [Fact]
    public void FqlAggregate_NoAlias_GeneratesDefaultAlias()
    {
        var result = FqlParse(aggregates: "AVG(Price)");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Avg);
        result.Aggregates[0].Field.Should().Be("Price");
        result.Aggregates[0].Alias.Should().Be("PriceAvg");
    }

    [Fact]
    public void FqlAggregate_NestedField_GeneratesCorrectAlias()
    {
        var result = FqlParse(aggregates: "SUM(Orders.Total)");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Sum);
        result.Aggregates[0].Field.Should().Be("Orders");
        result.Aggregates[0].Alias.Should().Be("OrdersTotalSum");
    }

    [Fact]
    public void FqlAggregate_CaseInsensitiveFunctions_ParsedCorrectly()
    {
        var result = FqlParse(aggregates: "sum(Amount), COUNT(Id), Avg(Price)");

        result.Aggregates.Should().HaveCount(3);
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Sum);
        result.Aggregates[1].Function.Should().Be(AggregateFunction.Count);
        result.Aggregates[2].Function.Should().Be(AggregateFunction.Avg);
    }

    [Fact]
    public void FqlAggregate_MinMax_ParsedCorrectly()
    {
        var result = FqlParse(aggregates: "MIN(Date), MAX(Date)");

        result.Aggregates.Should().HaveCount(2);
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Min);
        result.Aggregates[0].Alias.Should().Be("DateMin");
        result.Aggregates[1].Function.Should().Be(AggregateFunction.Max);
        result.Aggregates[1].Alias.Should().Be("DateMax");
    }

    [Fact]
    public void FqlAggregate_WithAlias_ExplicitlyProvided()
    {
        var result = FqlParse(aggregates: "SUM(Amount) AS TotalSales");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Sum);
        result.Aggregates[0].Field.Should().Be("Amount");
        result.Aggregates[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void FqlAggregate_EmptyAlias_IsRejected()
    {
        var act = () => FqlParse(aggregates: "SUM(Amount) AS");

        act.Should().Throw<QueryParseException>()
            .Which.ParameterName.Should().Be("aggregate");
    }

    [Fact]
    public void FqlAggregate_NoAlias_AutoGenerated()
    {
        var result = FqlParse(aggregates: "SUM(Amount)");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Sum);
        result.Aggregates[0].Field.Should().Be("Amount");
        result.Aggregates[0].Alias.Should().Be("AmountSum");
    }

    [Fact]
    public void FqlAggregate_WhitespaceVariations_Handled()
    {
        var result = FqlParse(aggregates: "SUM(Amount) AS TotalSum");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Alias.Should().Be("TotalSum");
    }

    [Fact]
    public void FqlAggregate_MultipleWithMixedAliases_ParsedCorrectly()
    {
        var result = FqlParse(aggregates: "SUM(Amount) AS TotalSales, COUNT(Orders) AS OrderCount, AVG(Price)");

        result.Aggregates.Should().HaveCount(3);
        result.Aggregates[0].Alias.Should().Be("TotalSales");
        result.Aggregates[1].Alias.Should().Be("OrderCount");
        result.Aggregates[2].Alias.Should().Be("PriceAvg");
    }

    [Fact]
    public void FqlAggregate_CountStar_NoAlias_ParsedCorrectly()
    {
        var result = FqlParse(aggregates: "COUNT(Orders)");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Count);
        result.Aggregates[0].Field.Should().Be("Orders");
        result.Aggregates[0].Alias.Should().Be("OrdersCount");
    }

    [Fact]
    public void FqlAggregate_MissingOpeningParen_IsRejected()
    {
        var act = () => FqlParse(aggregates: "SUM Total");

        act.Should().Throw<QueryParseException>()
            .Which.ParameterName.Should().Be("aggregate");
    }

    [Fact]
    public void FqlAggregate_MissingClosingParen_IsRejected()
    {
        var act = () => FqlParse(aggregates: "SUM(Total");

        act.Should().Throw<QueryParseException>()
            .Which.ParameterName.Should().Be("aggregate");
    }

    [Fact]
    public void FqlAggregate_MissingField_IsRejected()
    {
        var act = () => FqlParse(aggregates: "SUM()");

        act.Should().Throw<QueryParseException>()
            .Which.ParameterName.Should().Be("aggregate");
    }

    [Fact]
    public void FqlAggregate_EmptyAggregate_ReturnsEmptyList()
    {
        var result = FqlParse(aggregates: "");

        result.Aggregates.Should().BeEmpty();
    }

    [Fact]
    public void FqlAggregate_ExtraContentAfterField_IsRejected()
    {
        var act = () => FqlParse(aggregates: "SUM(Total))");

        act.Should().Throw<QueryParseException>()
            .Which.ParameterName.Should().Be("aggregate");
    }

    // â”€â”€â”€ GroupBy Parser Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FqlGroupBy_SingleField_ParsedCorrectly()
    {
        var result = FqlParse(groupBy: "Department");

        result.GroupBy.Should().ContainSingle();
        result.GroupBy[0].Should().Be("Department");
    }

    [Fact]
    public void FqlGroupBy_MultipleFields_ParsedCorrectly()
    {
        var result = FqlParse(groupBy: "Department,Category");

        result.GroupBy.Should().HaveCount(2);
        result.GroupBy.Should().ContainInOrder("Department", "Category");
    }

    [Fact]
    public void FqlGroupBy_NestedProperty_ParsedCorrectly()
    {
        var result = FqlParse(groupBy: "Customer.Region");

        result.GroupBy.Should().ContainSingle();
        result.GroupBy[0].Should().Be("Customer.Region");
    }

    // â”€â”€â”€ Having Parser Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FqlHaving_CountGreaterThan_ParsedCorrectly()
    {
        var result = FqlParse(having: "COUNT(Orders) > 5");

        result.Having.Should().NotBeNull();
        result.Having!.Function.Should().Be(AggregateFunction.Count);
        result.Having.Field.Should().Be("Orders");
        result.Having.Operator.Should().Be("gt");
        result.Having.Value.Should().Be("5");
    }

    [Fact]
    public void FqlHaving_SumGreaterThanValue_ParsedCorrectly()
    {
        var result = FqlParse(having: "SUM(Amount) > 1000");

        result.Having.Should().NotBeNull();
        result.Having!.Function.Should().Be(AggregateFunction.Sum);
        result.Having.Field.Should().Be("Amount");
        result.Having.Operator.Should().Be("gt");
        result.Having.Value.Should().Be("1000");
    }

    [Fact]
    public void FqlHaving_AllOperators_ParsedCorrectly()
    {
        AssertHaving("COUNT(Orders) > 5", "gt", "5");
        AssertHaving("SUM(Amount) >= 1000", "gte", "1000");
        AssertHaving("AVG(Price) < 50", "lt", "50");
        AssertHaving("SUM(Amount) <= 100", "lte", "100");
        AssertHaving("COUNT(Orders) = 0", "eq", "0");
        AssertHaving("COUNT(Orders) != 0", "neq", "0");
    }

    private void AssertHaving(string havingExpr, string expectedOp, string expectedValue)
    {
        var result = FqlParse(having: havingExpr);
        result.Having.Should().NotBeNull();
        result.Having!.Operator.Should().Be(expectedOp);
        result.Having.Value.Should().Be(expectedValue);
    }

    [Fact]
    public void FqlHaving_FieldSpecific_ParsedCorrectly()
    {
        var result = FqlParse(having: "SUM(Orders.Total) > 500");

        result.Having.Should().NotBeNull();
        result.Having!.Function.Should().Be(AggregateFunction.Sum);
        result.Having.Field.Should().Be("Orders.Total");
        result.Having.Operator.Should().Be("gt");
        result.Having.Value.Should().Be("500");
    }

    [Fact]
    public void FqlHaving_EmptyString_ReturnsNull()
    {
        var result = FqlParse(having: "");

        result.Having.Should().BeNull();
    }

    // â”€â”€â”€ Integration Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FqlIntegration_AllParameters_ParsedCorrectly()
    {
        var result = FqlParse(
            filter: "((status = 'Open' OR status = 'Pending') AND amount > 100) OR customer.name CONTAINS 'john'",
            sort: "createdAt DESC, name ASC",
            select: "Id,Name,CustomerName",
            include: "Orders,Profile",
            groupBy: "customerId,category",
            aggregates: "SUM(Amount) AS TotalSales, COUNT(Id), AVG(Price)",
            having: "SUM(Amount) > 1000",
            distinct: true,
            page: 2,
            pageSize: 25
        );

        // Filter â€” ((status = Open OR status = Pending) AND amount > 100) OR customer.name contains john
        result.Filter.Should().NotBeNull();
        result.Filter!.Logic.Should().Be(LogicOperator.Or);
        result.Filter.Filters.Should().ContainSingle(f => f.Field == "customer.name"
            && f.Operator == FilterOperators.Contains && f.Value == "john");
        result.Filter.Groups.Should().ContainSingle();
        var innerAnd = result.Filter.Groups[0];
        innerAnd.Logic.Should().Be(LogicOperator.And);
        innerAnd.Filters.Should().ContainSingle(f => f.Field == "amount"
            && f.Operator == FilterOperators.GreaterThan && f.Value == "100");
        innerAnd.Groups.Should().ContainSingle();
        var innerOr = innerAnd.Groups[0];
        innerOr.Logic.Should().Be(LogicOperator.Or);
        innerOr.Filters.Should().HaveCount(2);
        innerOr.Filters.Should().Contain(f => f.Field == "status" && f.Value == "Open");
        innerOr.Filters.Should().Contain(f => f.Field == "status" && f.Value == "Pending");

        // Sort
        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("createdAt");
        result.Sort[0].Descending.Should().BeTrue();
        result.Sort[1].Field.Should().Be("name");
        result.Sort[1].Descending.Should().BeFalse();

        // Select
        result.Select.Should().BeEquivalentTo([new SelectModel { Field = "Id" }, new SelectModel { Field = "Name" }, new SelectModel { Field = "CustomerName" }]);

        // Include
        result.Includes.Should().BeEquivalentTo(["Orders", "Profile"]);

        // GroupBy
        result.GroupBy.Should().BeEquivalentTo(["customerId", "category"]);

        // Aggregates
        result.Aggregates.Should().HaveCount(3);
        result.Aggregates[0].Function.Should().Be(AggregateFunction.Sum);
        result.Aggregates[0].Field.Should().Be("Amount");
        result.Aggregates[0].Alias.Should().Be("TotalSales");
        result.Aggregates[1].Function.Should().Be(AggregateFunction.Count);
        result.Aggregates[1].Field.Should().Be("Id");
        result.Aggregates[1].Alias.Should().Be("IdCount");
        result.Aggregates[2].Function.Should().Be(AggregateFunction.Avg);
        result.Aggregates[2].Field.Should().Be("Price");
        result.Aggregates[2].Alias.Should().Be("PriceAvg");

        // Having
        result.Having.Should().NotBeNull();
        result.Having!.Function.Should().Be(AggregateFunction.Sum);
        result.Having.Field.Should().Be("Amount");
        result.Having.Operator.Should().Be("gt");
        result.Having.Value.Should().Be("1000");

        // Distinct
        result.Distinct.Should().BeTrue();

        // Paging
        result.Paging.Page.Should().Be(2);
        result.Paging.PageSize.Should().Be(25);
    }
}

