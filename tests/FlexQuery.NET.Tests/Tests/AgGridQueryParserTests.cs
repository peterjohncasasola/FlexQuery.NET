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

    private static JsonElement Element(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }
}
