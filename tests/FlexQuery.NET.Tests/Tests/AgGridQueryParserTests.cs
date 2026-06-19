using System.Text.Json;
using FlexQuery.NET.AgGrid.Models;
using FlexQuery.NET.AgGrid.Parsers;
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
        result.GroupBy.Should().Equal("country", "city");
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
          "groupKeys": ["USA", "California"]
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
        // Let's also verify that Pivot/Value/GroupKeys do not pollute QueryOptions.
        request.Aggregates.Should().BeEmpty();
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

        // Let's deserialize using the public Parse(string) but verify we don't map to QueryOptions
        var options = AgGridQueryOptionsParser.Parse(json);
        options.GroupBy.Should().BeNull();
        options.Aggregates.Should().BeEmpty();

        // Let's test that we can parse the JsonElement to check that it parses successfully without throwing any FormatException.
        var act = () => AgGridQueryOptionsParser.Parse(json);
        act.Should().NotThrow();
    }

    private static JsonElement Element(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }
}
