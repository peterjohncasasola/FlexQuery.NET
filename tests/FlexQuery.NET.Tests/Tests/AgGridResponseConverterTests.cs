using FlexQuery.NET.Adapters.AgGrid;
using FlexQuery.NET.Adapters.AgGrid.Converters;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Models;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Tests;

public class AgGridResponseConverterTests
{
    [Fact]
    public void Convert_RootGrouping_MapsSingleAggregateAliasToField()
    {
        var request = new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Field = "category" },
                new() { Field = "brand" }
            ],
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = "sum" }
            ]
        };

        var result = new QueryResult<object>
        {
            TotalCount = 2,
            Data =
            [
                new { category = "Automotive", quantitySum = 998, childCount = 2 },
                new { category = "Electronics", quantitySum = 450, childCount = 3 }
            ]
        };

        var response = AgGridResponseConverter.Convert(request, result);

        response.RowCount.Should().Be(2);
        response.RowData.Should().HaveCount(2);
        response.RowData[0]["group"].Should().Be(true);
        response.RowData[0]["key"].Should().Be("Automotive");
        response.RowData[0]["field"].Should().Be("category");
        response.RowData[0]["leafGroup"].Should().Be(false);
        response.RowData[0]["childCount"].Should().Be(2);
        response.RowData[0]["quantity"].Should().Be(998);
        response.RowData[0].Should().NotContainKey("quantitySum");
    }

    [Fact]
    public void Convert_NestedGrouping_ProducesLeafGroupRowsForFinalLevel()
    {
        var request = new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Field = "category" },
                new() { Field = "brand" }
            ],
            GroupKeys = ["Automotive"],
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = "sum" }
            ]
        };

        var result = new QueryResult<object>
        {
            TotalCount = 2,
            Data =
            [
                new { brand = "Globex", quantitySum = 365 },
                new { brand = "Innotech", quantitySum = 633 }
            ]
        };

        var response = result.ToAgGridServerSideResponse(request);

        response.RowData.Should().HaveCount(2);
        response.RowData[0]["group"].Should().Be(true);
        response.RowData[0]["key"].Should().Be("Globex");
        response.RowData[0]["field"].Should().Be("brand");
        response.RowData[0]["leafGroup"].Should().Be(true);
        response.RowData[0]["groupKeys"].Should().BeAssignableTo<IReadOnlyList<string>>();
        ((IReadOnlyList<string>)response.RowData[0]["groupKeys"]!).Should().Equal("Automotive", "Globex");
        response.RowData[0]["quantity"].Should().Be(365);
        response.RowData[0].Should().NotContainKey("quantitySum");
    }

    [Fact]
    public void Convert_LeafLevel_PreservesLeafRowsWithoutGroupMetadata()
    {
        var request = new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Field = "category" },
                new() { Field = "brand" }
            ],
            GroupKeys = ["Automotive", "Globex"]
        };

        var result = new QueryResult<object>
        {
            TotalCount = 2,
            Data =
            [
                new { id = 1, category = "Automotive", brand = "Globex", quantity = 100 },
                new { id = 2, category = "Automotive", brand = "Globex", quantity = 265 }
            ]
        };

        var response = AgGridResponseConverter.Convert(request, result);

        response.RowData.Should().HaveCount(2);
        response.RowData[0].Should().ContainKey("id");
        response.RowData[0].Should().NotContainKey("group");
        response.RowData[0]["brand"].Should().Be("Globex");
    }

    [Fact]
    public void Convert_GroupedRow_KeepsAliasesForMultiAggregateFields()
    {
        var request = new AgGridRequest
        {
            RowGroupCols = [new() { Field = "category" }],
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = "sum" },
                new() { Field = "quantity", AggFunc = "avg" },
                new() { Field = "quantity", AggFunc = "min" },
                new() { Field = "quantity", AggFunc = "max" },
                new() { Field = "quantity", AggFunc = "count" }
            ]
        };

        var result = new QueryResult<object>
        {
            TotalCount = 1,
            Data =
            [
                new
                {
                    category = "Electronics",
                    quantitySum = 100,
                    quantityAvg = 25,
                    quantityMin = 10,
                    quantityMax = 40,
                    quantityCount = 4
                }
            ]
        };

        var response = AgGridResponseConverter.Convert(request, result);

        response.RowData.Should().ContainSingle();
        response.RowData[0].Should().Contain(new Dictionary<string, object?>
        {
            ["group"] = true,
            ["key"] = "Electronics",
            ["quantitySum"] = 100,
            ["quantityAvg"] = 25,
            ["quantityMin"] = 10,
            ["quantityMax"] = 40,
            ["quantityCount"] = 4
        });
        response.RowData[0].Should().NotContainKey("quantity");
    }

    [Fact]
    public void Convert_NoGrouping_ReturnsRowsUnchanged()
    {
        var request = new AgGridRequest();
        var result = new QueryResult<object>
        {
            TotalCount = 1,
            Data =
            [
                new { id = 1, category = "Electronics", quantity = 100 }
            ]
        };

        var response = AgGridResponseConverter.Convert(request, result);

        response.RowCount.Should().Be(1);
        response.RowData.Should().ContainSingle();
        response.RowData[0].Should().Contain(new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["category"] = "Electronics",
            ["quantity"] = 100
        });
        response.RowData[0].Should().NotContainKeys("group", "key", "field");
    }

    [Fact]
    public void Convert_Uses_Custom_Field_Names()
    {
        var request = new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Field = "category" },
                new() { Field = "brand" }
            ]
        };
        var result = new QueryResult<object>
        {
            TotalCount = 1,
            Data =
            [
                new { category = "Electronics", childTotal = 25 }
            ]
        };
        var options = new AgGridResponseFieldOptions
        {
            GroupFlagFieldName = "__group",
            KeyFieldName = "__key",
            FieldFieldName = "__field",
            LevelFieldName = "__level",
            LeafGroupFieldName = "__leaf",
            RouteFieldName = "__route",
            ChildCountFieldName = "__count",
            ChildCountSourceField = "childTotal"
        };

        var response = AgGridResponseConverter.Convert(request, result, options);

        response.RowData.Should().ContainSingle();
        response.RowData[0].Should().Contain(new Dictionary<string, object?>
        {
            ["__group"] = true,
            ["__key"] = "Electronics",
            ["__field"] = "category",
            ["__level"] = 0,
            ["__leaf"] = false,
            ["__count"] = 25
        });
        response.RowData[0].Should().ContainKey("__route");
        response.RowData[0].Should().NotContainKeys(
            "group",
            "key",
            "field",
            "level",
            "leafGroup",
            "groupKeys",
            "childCount");
    }

    [Fact]
    public void Convert_Uses_ChildCountSourceField()
    {
        var request = new AgGridRequest
        {
            RowGroupCols = [new() { Field = "category" }]
        };
        var result = new QueryResult<object>
        {
            TotalCount = 1,
            Data =
            [
                new { category = "Electronics", quantityCount = 25 }
            ]
        };
        var options = new AgGridResponseFieldOptions
        {
            ChildCountSourceField = "quantityCount"
        };

        var response = AgGridResponseConverter.Convert(request, result, options);

        response.RowData.Should().ContainSingle();
        response.RowData[0]["quantityCount"].Should().Be(25);
        response.RowData[0]["childCount"].Should().Be(25);
    }

    [Fact]
    public void Convert_GroupedRow_MapsSingleAggregateAliasToFieldName()
    {
        var request = new AgGridRequest
        {
            RowGroupCols = [new() { Field = "category" }],
            ValueCols =
            [
                new() { Field = "total", AggFunc = "sum" },
                new() { Field = "id", AggFunc = "count" }
            ]
        };

        var result = new QueryResult<object>
        {
            TotalCount = 1,
            Data =
            [
                new { category = "Food", totalSum = 1500m, idCount = 42 }
            ]
        };

        var response = AgGridResponseConverter.Convert(request, result);

        response.RowData.Should().ContainSingle();
        response.RowData[0].Should().Contain(new Dictionary<string, object?>
        {
            ["group"] = true,
            ["key"] = "Food",
            ["total"] = 1500m,
            ["id"] = 42
        });
        response.RowData[0].Should().NotContainKeys("totalSum", "idCount");
    }

    [Fact]
    public void Convert_WithAverageAggFunc_NormalizesToBuildAggregateAlias()
    {
        var request = new AgGridRequest
        {
            RowGroupCols = [new() { Field = "category" }],
            ValueCols =
            [
                new() { Field = "price", AggFunc = "average" }
            ]
        };

        var result = new QueryResult<object>
        {
            TotalCount = 1,
            Data =
            [
                new { category = "Food", priceAvg = 25.5m }
            ]
        };

        var response = AgGridResponseConverter.Convert(request, result);

        response.RowData[0]["price"].Should().Be(25.5m);
        response.RowData[0].Should().NotContainKey("priceAvg");
    }

    [Fact]
    public void Convert_WithoutValueCols_PreservesAllRowProperties()
    {
        var request = new AgGridRequest
        {
            RowGroupCols = [new() { Field = "category" }]
        };

        var result = new QueryResult<object>
        {
            TotalCount = 1,
            Data =
            [
                new { category = "Food", quantitySum = 100, quantityAvg = 25 }
            ]
        };

        var response = AgGridResponseConverter.Convert(request, result);

        response.RowData[0]["quantitySum"].Should().Be(100);
        response.RowData[0]["quantityAvg"].Should().Be(25);
    }

    [Fact]
    public void Convert_SingleAggregate_DoesNotOverwriteExistingField()
    {
        var request = new AgGridRequest
        {
            RowGroupCols = [new() { Field = "id" }],
            ValueCols =
            [
                new() { Field = "id", AggFunc = "count" }
            ]
        };

        var result = new QueryResult<object>
        {
            TotalCount = 1,
            Data =
            [
                new { id = 42, idCount = 10 }
            ]
        };

        var response = AgGridResponseConverter.Convert(request, result);

        response.RowData[0]["id"].Should().Be(42);
        response.RowData[0]["idCount"].Should().Be(10);
    }
}
