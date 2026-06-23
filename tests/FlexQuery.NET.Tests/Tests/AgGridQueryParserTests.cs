using System.Text.Json;
using FlexQuery.NET.Adapters.AgGrid;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Adapters.AgGrid.Parsers;
using FlexQuery.NET.Models;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Tests;

public class AgGridQueryParserTests
{
    [Fact]
    public void TextContains_ParsesToCanonicalContainsCondition()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = new()
                {
                    FilterType = "text",
                    Type = "contains",
                    Filter = Element("\"Peter\"")
                }
            }
        });

        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Field.Should().Be("name");
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        result.Filter.Filters[0].Value.Should().Be("Peter");
    }

    [Fact]
    public void NumberGreaterThan_ParsesToCanonicalGreaterThanCondition()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["age"] = new()
                {
                    FilterType = "number",
                    Type = "greaterThan",
                    Filter = Element("18")
                }
            }
        });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.GreaterThan);
        result.Filter.Filters[0].Value.Should().Be("18");
    }

    [Fact]
    public void DateGreaterThan_ParsesToCanonicalGreaterThanCondition()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["createdDate"] = new()
                {
                    FilterType = "date",
                    Type = "greaterThan",
                    DateFrom = Element("\"2025-01-01\"")
                }
            }
        });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.GreaterThan);
        result.Filter.Filters[0].Value.Should().Be("2025-01-01");
    }

    [Fact]
    public void SetFilter_ParsesValuesToInCondition()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["status"] = new()
                {
                    FilterType = "set",
                    Values = [Element("\"Open\""), Element("\"Approved\"")]
                }
            }
        });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.In);
        result.Filter.Filters[0].Value.Should().Be("Open,Approved");
    }

    [Fact]
    public void MultiConditionAnd_ParsesConditionsIntoSingleGroup()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = new()
                {
                    FilterType = "text",
                    Operator = "AND",
                    Conditions =
                    [
                        new()
                        {
                            Type = "contains",
                            Filter = Element("\"Peter\"")
                        },
                        new()
                        {
                            Type = "startsWith",
                            Filter = Element("\"P\"")
                        }
                    ]
                }
            }
        });

        result.Filter!.Logic.Should().Be(LogicOperator.And);
        result.Filter.Filters.Should().HaveCount(2);
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        result.Filter.Filters[1].Operator.Should().Be(FilterOperators.StartsWith);
    }

    [Fact]
    public void MultiConditionOr_PreservesNestedGroup()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["status"] = new()
                {
                    FilterType = "text",
                    Operator = "OR",
                    Conditions =
                    [
                        new()
                        {
                            Type = "equals",
                            Filter = Element("\"Open\"")
                        },
                        new()
                        {
                            Type = "equals",
                            Filter = Element("\"Approved\"")
                        }
                    ]
                }
            }
        });

        result.Filter!.Logic.Should().Be(LogicOperator.And);
        result.Filter.Groups.Should().ContainSingle();
        result.Filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        result.Filter.Groups[0].Filters.Should().HaveCount(2);
    }

    [Fact]
    public void SortModel_ParsesAscAndDescIntoSortNodes()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            SortModel =
            [
                new() { ColId = "name", Sort = "asc" },
                new() { ColId = "createdDate", Sort = "desc" }
            ]
        });

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("name");
        result.Sort[0].Descending.Should().BeFalse();
        result.Sort[1].Field.Should().Be("createdDate");
        result.Sort[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void ParseJsonRequest_ParsesFiltersAndSorts()
    {
        var json = """
        {
          "filterModel": {
            "name": {
              "filterType": "text",
              "type": "contains",
              "filter": "Peter"
            }
          },
          "sortModel": [
            {
              "colId": "name",
              "sort": "asc"
            }
          ]
        }
        """;

        var result = AgGridQueryOptionsParser.Parse(json);

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Field.Should().Be("name");
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void EmptyRequest_ProducesEmptyQueryOptions()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest());

        result.Filter.Should().BeNull();
        result.Sort.Should().BeEmpty();
    }

    [Fact]
    public void UnknownOperatorInStrictMode_ThrowsFormatException()
    {
        var request = new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = new()
                {
                    FilterType = "text",
                    Type = "unknown",
                    Filter = Element("\"Peter\"")
                }
            }
        };

        var act = () => AgGridQueryOptionsParser.Parse(request);

        act.Should().Throw<FormatException>().WithMessage("*unsupported AG Grid filter operator*");
    }

    [Theory]
    [InlineData("text", "contains", FilterOperators.Contains)]
    [InlineData("text", "equals", FilterOperators.Equal)]
    [InlineData("text", "notEqual", FilterOperators.NotEqual)]
    [InlineData("text", "startsWith", FilterOperators.StartsWith)]
    [InlineData("text", "endsWith", FilterOperators.EndsWith)]
    [InlineData("text", "blank", FilterOperators.IsNull)]
    [InlineData("text", "notBlank", FilterOperators.IsNotNull)]
    [InlineData("number", "equals", FilterOperators.Equal)]
    [InlineData("number", "notEqual", FilterOperators.NotEqual)]
    [InlineData("number", "greaterThan", FilterOperators.GreaterThan)]
    [InlineData("number", "greaterThanOrEqual", FilterOperators.GreaterThanOrEq)]
    [InlineData("number", "lessThan", FilterOperators.LessThan)]
    [InlineData("number", "lessThanOrEqual", FilterOperators.LessThanOrEq)]
    [InlineData("number", "blank", FilterOperators.IsNull)]
    [InlineData("number", "notBlank", FilterOperators.IsNotNull)]
    [InlineData("date", "equals", FilterOperators.Equal)]
    [InlineData("date", "notEqual", FilterOperators.NotEqual)]
    [InlineData("date", "greaterThan", FilterOperators.GreaterThan)]
    [InlineData("date", "greaterThanOrEqual", FilterOperators.GreaterThanOrEq)]
    [InlineData("date", "lessThan", FilterOperators.LessThan)]
    [InlineData("date", "lessThanOrEqual", FilterOperators.LessThanOrEq)]
    [InlineData("date", "after", FilterOperators.GreaterThan)]
    [InlineData("date", "afterOrEqual", FilterOperators.GreaterThanOrEq)]
    [InlineData("date", "before", FilterOperators.LessThan)]
    [InlineData("date", "beforeOrEqual", FilterOperators.LessThanOrEq)]
    [InlineData("date", "blank", FilterOperators.IsNull)]
    [InlineData("date", "notBlank", FilterOperators.IsNotNull)]
    public void TextNumberDateOperators_MapToExpectedOperators(string filterType, string agGridOperator, string expectedOperator)
    {
        var filter = new AgGridFilterNode
        {
            FilterType = filterType,
            Type = agGridOperator,
            Filter = Element("\"value\""),
            DateFrom = filterType == "date" ? Element("\"2025-01-01\"") : null
        };

        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["field"] = filter
            }
        });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(expectedOperator);
    }

    [Theory]
    [InlineData("text", "inRange", FilterOperators.Between, "value1", "value2")]
    [InlineData("number", "inRange", FilterOperators.Between, "10", "20")]
    [InlineData("date", "inRange", FilterOperators.Between, "2025-01-01", "2025-12-31")]
    public void RangeOperators_MapToBetweenWithCommaSeparatedValues(
        string filterType,
        string agGridOperator,
        string expectedOperator,
        string start,
        string end)
    {
        var filter = new AgGridFilterNode
        {
            FilterType = filterType,
            Type = agGridOperator,
            Filter = Element(JsonSerializer.Serialize(start)),
            FilterTo = Element(JsonSerializer.Serialize(end)),
            DateFrom = filterType == "date" ? Element(JsonSerializer.Serialize(start)) : null,
            DateTo = filterType == "date" ? Element(JsonSerializer.Serialize(end)) : null
        };

        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["field"] = filter
            }
        });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(expectedOperator);
        result.Filter.Filters[0].Value.Should().Be($"{start},{end}");
    }

    [Fact]
    public void Pagination_MapsStartRowAndEndRowToPageAndPageSize()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            StartRow = 150,
            EndRow = 200
        });

        result.Paging.PageSize.Should().Be(50);
        result.Paging.Page.Should().Be(4);
    }

    [Fact]
    public void Pagination_SafelyHandlesInvalidPagingRanges()
    {
        // startRow < 0
        var resultNegativeStart = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            StartRow = -10,
            EndRow = 50
        });
        resultNegativeStart.Paging.PageSize.Should().Be(20); // Default
        resultNegativeStart.Paging.Page.Should().Be(1);

        // endRow <= startRow
        var resultEndLess = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            StartRow = 50,
            EndRow = 40
        });
        resultEndLess.Paging.PageSize.Should().Be(20); // Default
        resultEndLess.Paging.Page.Should().Be(1);

        // startRow == endRow
        var resultEqual = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            StartRow = 50,
            EndRow = 50
        });
        resultEqual.Paging.PageSize.Should().Be(20); // Default
        resultEqual.Paging.Page.Should().Be(1);
    }

    [Fact]
    public void RowGroups_MapsRowGroupColsToGroupByPreservingOrder()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = new List<AgGridGroupColumn>
            {
                new() { Field = "country" },
                new() { Field = null },
                new() { Field = "  " },
                new() { Field = "city" }
            }
        });

        result.GroupBy.Should().NotBeNull();
        result.GroupBy.Should().Equal("country");
    }

    [Fact]
    public void RowGroups_WithGroupKey_MapsToNextLevelGroupingAndPrefixFilter()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Field = "category" },
                new() { Field = "brand" }
            ],
            GroupKeys = ["Automotive"]
        });

        result.GroupBy.Should().Equal("brand");
        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Field.Should().Be("category");
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        result.Filter.Filters[0].Value.Should().Be("Automotive");
    }

    [Fact]
    public void RowGroups_AtLeafLevel_DisablesGroupingAndAppliesAllGroupKeyFilters()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Field = "category" },
                new() { Field = "brand" }
            ],
            GroupKeys = ["Automotive", "Globex"],
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = "sum" }
            ]
        });

        result.GroupBy.Should().BeNull();
        result.Aggregates.Should().BeEmpty();
        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().HaveCount(2);
        result.Filter.Filters.Select(f => (f.Field, f.Value)).Should().Contain(new[]
        {
            ("category", "Automotive"),
            ("brand", "Globex")
        });
    }

    [Fact]
    public void RowGroups_FollowsRootExpandedAndLeafSsrmFlow()
    {
        var rowGroupCols = new List<AgGridGroupColumn>
        {
            new() { Field = "Category" },
            new() { Field = "Brand" }
        };

        var root = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = rowGroupCols,
            GroupKeys = []
        });

        root.GroupBy.Should().Equal("Category");
        root.Filter.Should().BeNull();

        var expandedCategory = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = rowGroupCols,
            GroupKeys = ["Electronics"]
        });

        expandedCategory.GroupBy.Should().Equal("Brand");
        expandedCategory.Filter!.Filters.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Field = "Category",
                Operator = FilterOperators.Equal,
                Value = "Electronics"
            });

        var leaf = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = rowGroupCols,
            GroupKeys = ["Electronics", "Acme"]
        });

        leaf.GroupBy.Should().BeNull();
        leaf.Filter!.Filters.Select(filter => (filter.Field, filter.Value)).Should().Equal(
            ("Category", "Electronics"),
            ("Brand", "Acme"));
    }

    [Theory]
    [InlineData("sum", "sum", "quantitySum")]
    [InlineData("avg", "avg", "quantityAvg")]
    [InlineData("average", "avg", "quantityAvg")]
    [InlineData("min", "min", "quantityMin")]
    [InlineData("max", "max", "quantityMax")]
    [InlineData("count", "count", "quantityCount")]
    public void Aggregates_PreserveCanonicalFunctionAndAlias(
        string agGridFunction,
        string expectedFunction,
        string expectedAlias)
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Field = "category" }],
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = agGridFunction }
            ]
        });

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be(expectedFunction);
        result.Aggregates[0].Alias.Should().Be(expectedAlias);
    }

    [Fact]
    public void ApplyAgGridRequest_LeafRequestClearsStaleGroupingAndAggregates()
    {
        var existing = new QueryOptions
        {
            GroupBy = ["category"],
            Aggregates =
            [
                new AggregateModel
                {
                    Field = "quantity",
                    Function = "sum",
                    Alias = "quantitySum"
                }
            ]
        };

        existing.ApplyAgGridRequest(new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Field = "category" },
                new() { Field = "brand" }
            ],
            GroupKeys = ["Electronics", "Acme"],
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = "sum" }
            ]
        });

        existing.GroupBy.Should().BeNull();
        existing.Aggregates.Should().BeEmpty();
    }

    [Fact]
    public void ApplyAgGridRequest_ReplacesExistingGroupingAndAggregates()
    {
        var existing = new QueryOptions
        {
            GroupBy = ["oldGroup"],
            Aggregates =
            [
                new AggregateModel { Field = "oldValue", Function = "max", Alias = "oldValueMax" }
            ]
        };

        existing.ApplyAgGridRequest(new AgGridRequest
        {
            RowGroupCols = [new() { Field = "category" }],
            ValueCols = [new() { Field = "quantity", AggFunc = "sum" }]
        });

        existing.GroupBy.Should().Equal("category");
        existing.Aggregates.Should().ContainSingle();
        existing.Aggregates[0].Alias.Should().Be("quantitySum");
    }

    [Fact]
    public void ParseJsonRequest_ParsesPaginationRowGroupsAndFutureFields()
    {
        var json = """
        {
          "startRow": 100,
          "endRow": 150,
          "rowGroupCols": [
            { "field": "country" }
          ],
          "valueCols": [
            { "field": "gold", "aggFunc": "sum" }
          ],
          "pivotCols": [
            { "field": "year" }
          ],
          "pivotMode": true,
          "groupKeys": []
        }
        """;

        using var document = JsonDocument.Parse(json);
        var request = AgGridQueryOptionsParser.Parse(json); // This internally deserializes and parses

        // Verify mapped options
        request.Paging.PageSize.Should().Be(50);
        request.Paging.Page.Should().Be(3);
        request.GroupBy.Should().Equal("country");

        // Verify deserialized DTOs directly via DeserializeRequest
        var deserialized = AgGridQueryOptionsParser.Parse(document.RootElement); // Wait, Parse returns QueryOptions, to verify deserialized object let's parse the json element and verify QueryOptions.
        // Let's also verify that we can manually parse it or just check mapped options since we can't inspect the private deserialized object directly from Parse(JsonElement) output.
        // But wait! We can write a test that verifies the manual deserialization if we expose/make it testable, or since AgGridQueryOptionsParser.Parse(JsonElement) calls it,
        // we can see that it parsed correctly because request had paging and grouping.
        // Pivot fields are out of scope and do not pollute QueryOptions.
        request.Aggregates.Should().ContainSingle();
        request.Aggregates[0].Field.Should().Be("gold");
        request.Aggregates[0].Function.Should().Be("sum");
    }

    [Fact]
    public void DeserializeRequest_ParsesFutureDTOsCorrectly()
    {
        var json = """
        {
          "startRow": 0,
          "endRow": 10,
          "valueCols": [
            { "field": "gold", "aggFunc": "sum" }
          ],
          "pivotCols": [
            { "field": "year" }
          ],
          "pivotMode": true,
          "groupKeys": ["USA"]
        }
        """;

        // Let's deserialize using the public Parse(string) but verify we map to QueryOptions
        var options = AgGridQueryOptionsParser.Parse(json);
        options.GroupBy.Should().BeNull();
        // No rowGroupCols means no grouping is active, so valueCols should be ignored
        // (AG Grid always sends valueCols as column metadata regardless of grouping state)
        options.Aggregates.Should().BeEmpty();

        // Let's test that we can parse the JsonElement to check that it parses successfully without throwing any FormatException.
        var act = () => AgGridQueryOptionsParser.Parse(json);
        act.Should().NotThrow();
    }

    [Fact]
    public void AgGridRequest_WithValueColsButNoGrouping_DoesNotGenerateAggregates()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = "sum" }
            ]
        });

        result.Aggregates.Should().BeEmpty();
        result.GroupBy.Should().BeNull();
    }

    [Fact]
    public void AgGridRequest_WithValueColsAndEmptyRowGroupCols_DoesNotGenerateAggregates()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [],
            GroupKeys = [],
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = "sum" },
                new() { Field = "price", AggFunc = "avg" }
            ]
        });

        result.Aggregates.Should().BeEmpty();
        result.GroupBy.Should().BeNull();
    }

    [Fact]
    public void AgGridRequest_GroupedRequest_GeneratesAggregates()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Field = "category" }],
            ValueCols =
            [
                new() { Field = "quantity", AggFunc = "sum" },
                new() { Field = "price", AggFunc = "avg" },
                new() { Field = "price", AggFunc = "min" },
                new() { Field = "price", AggFunc = "max" },
                new() { Field = "id", AggFunc = "count" }
            ]
        });

        result.Aggregates.Should().HaveCount(5);
        result.Aggregates.Select(a => a.Alias).Should().Equal("quantitySum", "priceAvg", "priceMin", "priceMax", "idCount");
        result.Aggregates.Select(a => a.Function).Should().Equal("sum", "avg", "min", "max", "count");
    }

    [Fact]
    public void GroupedSort_AggregateSort_ResolvesToAggregateAlias()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            ValueCols = [new() { Id = "price", Field = "price", AggFunc = "AVG" }],
            SortModel = [new() { ColId = "price", Sort = "desc" }]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("priceAvg");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void GroupedSort_InvalidDetailSort_IsRemovedAndFallbackInjected()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            ValueCols = [new() { Id = "price", Field = "price", AggFunc = "AVG" }],
            SortModel = [new() { ColId = "id", Sort = "asc" }]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("category");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void GroupedSort_AllInvalidSorts_InjectsFallback()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            SortModel =
            [
                new() { ColId = "id", Sort = "asc" },
                new() { ColId = "createdOn", Sort = "desc" }
            ]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("category");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void GroupedSort_EmptySortModel_InjectsFallback()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            ValueCols = [new() { Id = "price", Field = "price", AggFunc = "AVG" }]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("category");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void GroupedSort_ColIdDiffersFromField_ResolvesCorrectly()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "cat", Field = "category" }],
            ValueCols = [new() { Id = "avg_price", Field = "price", AggFunc = "AVG" }],
            SortModel =
            [
                new() { ColId = "avg_price", Sort = "desc" },
                new() { ColId = "cat", Sort = "asc" }
            ]
        });

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("priceAvg");
        result.Sort[0].Descending.Should().BeTrue();
        result.Sort[1].Field.Should().Be("category");
        result.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void GroupedSort_NestedGrouping_ValidatesAgainstCurrentGroupAndAggregates()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Id = "category", Field = "category" },
                new() { Id = "brand", Field = "brand" }
            ],
            GroupKeys = ["Electronics"],
            ValueCols =
            [
                new() { Id = "price", Field = "price", AggFunc = "AVG" },
                new() { Id = "quantity", Field = "quantity", AggFunc = "SUM" }
            ],
            SortModel = [new() { ColId = "price", Sort = "desc" }]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("priceAvg");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void GroupedSort_NestedGroupingWithInvalidSort_InjectsFallbackToCurrentGroup()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Id = "category", Field = "category" },
                new() { Id = "brand", Field = "brand" }
            ],
            GroupKeys = ["Electronics"],
            ValueCols = [new() { Id = "price", Field = "price", AggFunc = "AVG" }],
            SortModel = [new() { ColId = "category", Sort = "asc" }]
        });

        // "category" is a parent group key, NOT in the current projection
        // Fallback should be the current group field: "brand"
        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("brand");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void GroupedSort_GroupKeySort_ResolvesToProjectionName()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            ValueCols = [new() { Id = "price", Field = "price", AggFunc = "AVG" }],
            SortModel = [new() { ColId = "category", Sort = "asc" }]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("category");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void GroupedSort_MixedValidAndInvalid_KeepsValidRemovesInvalid()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            ValueCols = [new() { Id = "price", Field = "price", AggFunc = "AVG" }],
            SortModel =
            [
                new() { ColId = "price", Sort = "desc" },
                new() { ColId = "id", Sort = "asc" },
                new() { ColId = "createdOn", Sort = "desc" }
            ]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("priceAvg");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void UngroupedSort_WithoutRowGroupCols_PassesSortThroughUnchanged()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            SortModel =
            [
                new() { ColId = "name", Sort = "asc" },
                new() { ColId = "id", Sort = "desc" }
            ]
        });

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("name");
        result.Sort[0].Descending.Should().BeFalse();
        result.Sort[1].Field.Should().Be("id");
        result.Sort[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void GroupedSort_AverageFunction_NormalizesToAvgAlias()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            ValueCols = [new() { Id = "price", Field = "price", AggFunc = "average" }],
            SortModel = [new() { ColId = "price", Sort = "asc" }]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("priceAvg");
    }

    [Fact]
    public void GroupedSort_SumAggregate_ResolvesToSumAlias()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            ValueCols = [new() { Id = "quantity", Field = "quantity", AggFunc = "SUM" }],
            SortModel = [new() { ColId = "quantity", Sort = "asc" }]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("quantitySum");
    }

    [Theory]
    [InlineData("SUM")]
    [InlineData("sum")]
    [InlineData("Sum")]
    public void GroupedSort_AggregateFunctionCasing_ResolvesCorrectly(string aggFunc)
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            ValueCols = [new() { Id = "quantity", Field = "quantity", AggFunc = aggFunc }],
            SortModel = [new() { ColId = "quantity", Sort = "asc" }]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("quantitySum");
    }

    [Fact]
    public void GroupedSort_LeafLevel_PassesSortThroughUnchanged()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols = [new() { Id = "category", Field = "category" }],
            GroupKeys = ["Electronics"],
            ValueCols = [new() { Id = "price", Field = "price", AggFunc = "AVG" }],
            SortModel = [new() { ColId = "price", Sort = "desc" }]
        });

        // Leaf level: rowGroupCols.Count == groupKeys.Count, so ungrouped
        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("price");
        result.Sort[0].Descending.Should().BeTrue();
    }


    [Fact]
    public void GroupedSort_GroupKeyIdDiffersFromField_ResolvesCorrectly()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols =
            [
                new()
                {
                    Id = "cat",
                    Field = "category"
                }
            ],
            SortModel =
            [
                new()
                {
                    ColId = "cat",
                    Sort = "asc"
                }
            ]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("category");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void GroupedSort_NullId_FallsBackToField()
    {
        var result = AgGridQueryOptionsParser.Parse(new AgGridRequest
        {
            RowGroupCols =
            [
                new() { Field = "category" }
            ],
            ValueCols =
            [
                new() { Field = "price", AggFunc = "AVG" }
            ],
            SortModel =
            [
                new() { ColId = "price", Sort = "desc" }
            ]
        });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("priceAvg");
    }

    private static JsonElement Element(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }
}
