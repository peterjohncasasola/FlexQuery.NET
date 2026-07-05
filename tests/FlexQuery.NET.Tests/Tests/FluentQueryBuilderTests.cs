using FlexQuery.NET.Models;

namespace FlexQuery.NET.Tests.Tests;

public class FluentQueryBuilderTests
{
    [Fact]
    public void QueryCreate_BuildsEmptyOptions()
    {
        var options = Query.Create().Build();

        options.Should().NotBeNull();
        options.Filter.Should().BeNull();
        options.Sort.Should().BeEmpty();
        options.Select.Should().BeNull();
        options.Includes.Should().BeNull();
        options.Expand.Should().BeNull();
        options.Paging.Page.Should().Be(1);
        options.Paging.PageSize.Should().Be(20);
        options.Distinct.Should().BeNull();
        options.IncludeCount.Should().BeTrue();
        options.CaseInsensitive.Should().BeTrue();
        options.ProjectionMode.Should().Be(ProjectionMode.Nested);
    }

    [Fact]
    public void QueryCreate_ImplicitConversionToQueryOptions()
    {
        QueryOptions? options = Query.Create();

        options.Should().NotBeNull();
    }

    [Fact]
    public void Where_SetsFilterGroup()
    {
        var options = Query.Create()
            .Where(f => f.Equal("Name", "John").GreaterThan("Age", 18))
            .Build();

        options.Filter.Should().NotBeNull();
        options.Filter!.Filters.Should().HaveCount(2);
        options.Filter.Filters[0].Field.Should().Be("Name");
        options.Filter.Filters[0].Operator.Should().Be("eq");
        options.Filter.Filters[0].Value.Should().Be("John");
        options.Filter.Filters[1].Field.Should().Be("Age");
        options.Filter.Filters[1].Operator.Should().Be("gt");
        options.Filter.Filters[1].Value.Should().Be("18");
    }

    [Fact]
    public void Where_WithNestedGroups_SetsFilterStructure()
    {
        var options = Query.Create()
            .Where(f => f
                .And(gb =>
                {
                    gb.Equal("Country", "USA");
                    gb.GreaterThan("Age", 18);
                })
                .Or(ob =>
                {
                    ob.Equal("Status", "Premium");
                }))
            .Build();

        options.Filter.Should().NotBeNull();
        options.Filter!.Groups.Should().HaveCount(2);
        options.Filter.Groups[0].Logic.Should().Be(LogicOperator.And);
        options.Filter.Groups[0].Filters.Should().HaveCount(2);
        options.Filter.Groups[1].Logic.Should().Be(LogicOperator.Or);
        options.Filter.Groups[1].Filters.Should().HaveCount(1);
    }

    [Fact]
    public void Where_WithAllFilterOperators_BuildsConditions()
    {
        var options = Query.Create()
            .Where(f => f
                .Equal("A", "x")
                .NotEqual("B", "y")
                .GreaterThan("C", 1)
                .GreaterThanOrEqual("D", 2)
                .LessThan("E", 3)
                .LessThanOrEqual("F", 4)
                .Contains("G", "sub")
                .StartsWith("H", "pre")
                .EndsWith("I", "suf")
                .In("J", "a", "b", "c")
                .NotIn("K", "d", "e")
                .IsNull("L")
                .IsNotNull("M")
                .Between("N", 10, 20))
            .Build();

        options.Filter.Should().NotBeNull();
        options.Filter!.Filters.Should().HaveCount(14);
        options.Filter.Filters[0].Should().Match<FilterCondition>(c => c.Field == "A" && c.Operator == "eq" && c.Value == "x");
        options.Filter.Filters[1].Should().Match<FilterCondition>(c => c.Field == "B" && c.Operator == "neq" && c.Value == "y");
        options.Filter.Filters[2].Should().Match<FilterCondition>(c => c.Field == "C" && c.Operator == "gt" && c.Value == "1");
        options.Filter.Filters[3].Should().Match<FilterCondition>(c => c.Field == "D" && c.Operator == "gte" && c.Value == "2");
        options.Filter.Filters[4].Should().Match<FilterCondition>(c => c.Field == "E" && c.Operator == "lt" && c.Value == "3");
        options.Filter.Filters[5].Should().Match<FilterCondition>(c => c.Field == "F" && c.Operator == "lte" && c.Value == "4");
        options.Filter.Filters[6].Should().Match<FilterCondition>(c => c.Field == "G" && c.Operator == "contains" && c.Value == "sub");
        options.Filter.Filters[7].Should().Match<FilterCondition>(c => c.Field == "H" && c.Operator == "startswith" && c.Value == "pre");
        options.Filter.Filters[8].Should().Match<FilterCondition>(c => c.Field == "I" && c.Operator == "endswith" && c.Value == "suf");
        options.Filter.Filters[9].Should().Match<FilterCondition>(c => c.Field == "J" && c.Operator == "in" && c.Value == "a,b,c");
        options.Filter.Filters[10].Should().Match<FilterCondition>(c => c.Field == "K" && c.Operator == "notin" && c.Value == "d,e");
        options.Filter.Filters[11].Should().Match<FilterCondition>(c => c.Field == "L" && c.Operator == "isnull" && c.Value == null);
        options.Filter.Filters[12].Should().Match<FilterCondition>(c => c.Field == "M" && c.Operator == "isnotnull" && c.Value == null);
        options.Filter.Filters[13].Should().Match<FilterCondition>(c => c.Field == "N" && c.Operator == "between" && c.Value == "10,20");
    }

    [Fact]
    public void Sort_BuildsSortNodes()
    {
        var options = Query.Create()
            .Sort(s => s.Ascending("Name").Descending("Age"))
            .Build();

        options.Sort.Should().HaveCount(2);
        options.Sort[0].Field.Should().Be("Name");
        options.Sort[0].Descending.Should().BeFalse();
        options.Sort[1].Field.Should().Be("Age");
        options.Sort[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void Select_SetsFieldList()
    {
        var options = Query.Create()
            .Select("Id", "Name", "Email")
            .Build();

        options.Select.Should().BeEquivalentTo(new[] { "Id", "Name", "Email" });
    }

    [Fact]
    public void Select_WithNoArgs_ReturnsNull()
    {
        var options = Query.Create()
            .Select()
            .Build();

        options.Select.Should().BeNull();
    }

    [Fact]
    public void Include_SetsIncludePaths()
    {
        var options = Query.Create()
            .Include("Orders", "Profile")
            .Build();

        options.Includes.Should().BeEquivalentTo(new[] { "Orders", "Profile" });
    }

    [Fact]
    public void Include_WithNoArgs_ReturnsNull()
    {
        var options = Query.Create()
            .Include()
            .Build();

        options.Includes.Should().BeNull();
    }

    [Fact]
    public void Expand_AddsFilteredIncludes()
    {
        var options = Query.Create()
            .Expand(e => e.Path("Orders", f => f.GreaterThan("Total", 100)))
            .Build();

        options.Expand.Should().HaveCount(1);
        options.Expand![0].Path.Should().Be("Orders");
        options.Expand[0].Filter.Should().NotBeNull();
        options.Expand[0].Filter!.Filters.Should().HaveCount(1);
        options.Expand[0].Filter?.Filters[0].Field.Should().Be("Total");
        options.Expand[0].Filter?.Filters[0].Operator.Should().Be("gt");
        options.Expand[0].Filter?.Filters[0].Value.Should().Be("100");
    }

    [Fact]
    public void Expand_WithChildren_BuildsNestedTree()
    {
        var options = Query.Create()
            .Expand(e => e.Path("Orders", configureChildren: c =>
                c.Path("Items")))
            .Build();

        options.Expand.Should().HaveCount(1);
        options.Expand![0].Path.Should().Be("Orders");
        options.Expand[0].Children.Should().HaveCount(1);
        options.Expand[0].Children[0].Path.Should().Be("Items");
    }

    [Fact]
    public void Expand_Empty_DoesNotAddFilteredIncludes()
    {
        var options = Query.Create()
            .Expand(e => { })
            .Build();

        options.Expand.Should().BeNull();
    }

    [Fact]
    public void Mode_SetsProjectionMode()
    {
        var flat = Query.Create().Mode(ProjectionMode.Flat).Build();
        var nested = Query.Create().Mode(ProjectionMode.Nested).Build();
        var mixed = Query.Create().Mode(ProjectionMode.FlatMixed).Build();

        flat.ProjectionMode.Should().Be(ProjectionMode.Flat);
        nested.ProjectionMode.Should().Be(ProjectionMode.Nested);
        mixed.ProjectionMode.Should().Be(ProjectionMode.FlatMixed);
    }

    [Fact]
    public void GroupBy_SetsGroupByFields()
    {
        var options = Query.Create()
            .GroupBy("Category", "Region")
            .Build();

        options.GroupBy.Should().BeEquivalentTo(new[] { "Category", "Region" });
    }

    [Fact]
    public void GroupBy_WithNoArgs_ReturnsNull()
    {
        var options = Query.Create()
            .GroupBy()
            .Build();

        options.GroupBy.Should().BeNull();
    }

    [Fact]
    public void Aggregate_AddsAggregateProjections()
    {
        var options = Query.Create()
            .Aggregate(a => a
                .Sum("Amount", "TotalAmount")
                .Count("Id", "OrderCount")
                .Count("TotalCount")
                .Avg("Price", "AvgPrice")
                .Min("Date", "Earliest")
                .Max("Date", "Latest")
                .Custom("DISTINCT", "Category", "DistinctCategories"))
            .Build();

        options.Aggregates.Should().HaveCount(7);
        options.Aggregates[0].Should().Match<AggregateModel>(a => a.Function == "sum" && a.Field == "Amount" && a.Alias == "TotalAmount");
        options.Aggregates[1].Should().Match<AggregateModel>(a => a.Function == "count" && a.Field == "Id" && a.Alias == "OrderCount");
        options.Aggregates[2].Should().Match<AggregateModel>(a => a.Function == "count" && a.Field == null && a.Alias == "TotalCount");
        options.Aggregates[3].Should().Match<AggregateModel>(a => a.Function == "avg" && a.Field == "Price" && a.Alias == "AvgPrice");
        options.Aggregates[4].Should().Match<AggregateModel>(a => a.Function == "min" && a.Field == "Date" && a.Alias == "Earliest");
        options.Aggregates[5].Should().Match<AggregateModel>(a => a.Function == "max" && a.Field == "Date" && a.Alias == "Latest");
        options.Aggregates[6].Should().Match<AggregateModel>(a => a.Function == "DISTINCT" && a.Field == "Category" && a.Alias == "DistinctCategories");
    }

    [Fact]
    public void Having_SetsHavingCondition()
    {
        var options = Query.Create()
            .Having("count", "Id", "gt", "5")
            .Build();

        options.Having.Should().NotBeNull();
        options.Having!.Function.Should().Be("count");
        options.Having.Field.Should().Be("Id");
        options.Having.Operator.Should().Be("gt");
        options.Having.Value.Should().Be("5");
    }

    [Fact]
    public void Distinct_SetsDistinct()
    {
        var enabled = Query.Create().Distinct().Build();
        var enabledExplicit = Query.Create().Distinct(true).Build();
        var disabled = Query.Create().Distinct(false).Build();

        enabled.Distinct.Should().BeTrue();
        enabledExplicit.Distinct.Should().BeTrue();
        disabled.Distinct.Should().BeFalse();
    }

    [Fact]
    public void Page_SetsPaging()
    {
        var options = Query.Create()
            .Page(3, 50)
            .Build();

        options.Paging.Page.Should().Be(3);
        options.Paging.PageSize.Should().Be(50);
    }

    [Fact]
    public void DisablePaging_DisablesPaging()
    {
        var options = Query.Create()
            .DisablePaging()
            .Build();

        options.Paging.Disabled.Should().BeTrue();
    }

    [Fact]
    public void FluentQueryBuilder_FullPipeline_BuildsCompleteOptions()
    {
        var options = Query.Create()
            .Where(f => f.Equal("Status", "Active").GreaterThan("Age", 21))
            .Sort(s => s.Ascending("Name"))
            .Select("Id", "Name", "Email")
            .Include("Orders")
            .Expand(e => e.Path("Orders", f => f.GreaterThan("Total", 100)))
            .Mode(ProjectionMode.Flat)
            .GroupBy("Category")
            .Aggregate(a => a.Sum("Amount", "Total"))
            .Having("sum", "Amount", "gt", "1000")
            .Distinct()
            .Page(2, 25)
            .Build();

        options.Filter.Should().NotBeNull();
        options.Sort.Should().HaveCount(1);
        options.Select.Should().HaveCount(3);
        options.Includes.Should().HaveCount(1);
        options.Expand.Should().HaveCount(1);
        options.ProjectionMode.Should().Be(ProjectionMode.Flat);
        options.GroupBy.Should().HaveCount(1);
        options.Aggregates.Should().HaveCount(1);
        options.Having.Should().NotBeNull();
        options.Distinct.Should().BeTrue();
        options.Paging.Page.Should().Be(2);
        options.Paging.PageSize.Should().Be(25);
    }
}