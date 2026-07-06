using System.Text.Json;
using FlexQuery.NET.Adapters.Kendo;
using FlexQuery.NET.Adapters.Kendo.Models;
using FlexQuery.NET.Adapters.Kendo.Parsers;
using FlexQuery.NET.Models;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Tests;

public class KendoQueryParserTests
{
    [Fact]
    public void TextContains_ParsesToCanonicalContainsCondition()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Filter = new KendoFilter
            {
                Logic = "and",
                Filters = new List<KendoFilterDescriptor>
                {
                    new()
                    {
                        Field = "name",
                        Operator = "contains",
                        Value = Element("\"Peter\"")
                    }
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
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Filter = new KendoFilter
            {
                Logic = "and",
                Filters = new List<KendoFilterDescriptor>
                {
                    new()
                    {
                        Field = "age",
                        Operator = "gt",
                        Value = Element("18")
                    }
                }
            }
        });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.GreaterThan);
        result.Filter.Filters[0].Value.Should().Be("18");
    }

    [Fact]
    public void EqualsOperator_ParsesToCanonicalEqualCondition()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Filter = new KendoFilter
            {
                Logic = "and",
                Filters = new List<KendoFilterDescriptor>
                {
                    new()
                    {
                        Field = "status",
                        Operator = "eq",
                        Value = Element("\"Active\"")
                    }
                }
            }
        });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        result.Filter.Filters[0].Value.Should().Be("Active");
    }

    [Fact]
    public void MultiConditionAnd_ParsesConditionsIntoSingleGroup()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Filter = new KendoFilter
            {
                Logic = "and",
                Filters = new List<KendoFilterDescriptor>
                {
                    new()
                    {
                        Field = "name",
                        Operator = "contains",
                        Value = Element("\"Peter\"")
                    },
                    new()
                    {
                        Field = "name",
                        Operator = "startswith",
                        Value = Element("\"P\"")
                    }
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
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Filter = new KendoFilter
            {
                Logic = "or",
                Filters = new List<KendoFilterDescriptor>
                {
                    new()
                    {
                        Field = "status",
                        Operator = "eq",
                        Value = Element("\"Open\"")
                    },
                    new()
                    {
                        Field = "status",
                        Operator = "eq",
                        Value = Element("\"Approved\"")
                    }
                }
            }
        });

        result.Filter!.Logic.Should().Be(LogicOperator.Or);
        result.Filter.Filters.Should().HaveCount(2);
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        result.Filter.Filters[1].Operator.Should().Be(FilterOperators.Equal);
    }

    [Fact]
    public void SortModel_ParsesAscAndDescIntoSortNodes()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Sort = new List<KendoSortDescriptor>
            {
                new() { Field = "name", Dir = "asc" },
                new() { Field = "createdDate", Dir = "desc" }
            }
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
          "filter": {
            "logic": "and",
            "filters": [
              {
                "field": "name",
                "operator": "contains",
                "value": "Peter"
              }
            ]
          },
          "sort": [
            {
              "field": "name",
              "dir": "asc"
            }
          ]
        }
        """;

        var result = KendoQueryOptionsParser.Parse(json);

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
        var result = KendoQueryOptionsParser.Parse(new KendoRequest());

        result.Filter.Should().BeNull();
        result.Sort.Should().BeEmpty();
    }

    [Fact]
    public void UnknownOperator_ThrowsFormatException()
    {
        var request = new KendoRequest
        {
            Filter = new KendoFilter
            {
                Logic = "and",
                Filters = new List<KendoFilterDescriptor>
                {
                    new()
                    {
                        Field = "name",
                        Operator = "unknown",
                        Value = Element("\"Peter\"")
                    }
                }
            }
        };

        var act = () => KendoQueryOptionsParser.Parse(request);

        act.Should().Throw<FormatException>().WithMessage("*unsupported Kendo filter operator*");
    }

    [Theory]
    [InlineData("eq", FilterOperators.Equal)]
    [InlineData("neq", FilterOperators.NotEqual)]
    [InlineData("contains", FilterOperators.Contains)]
    [InlineData("startswith", FilterOperators.StartsWith)]
    [InlineData("endswith", FilterOperators.EndsWith)]
    [InlineData("gt", FilterOperators.GreaterThan)]
    [InlineData("gte", FilterOperators.GreaterThanOrEq)]
    [InlineData("lt", FilterOperators.LessThan)]
    [InlineData("lte", FilterOperators.LessThanOrEq)]
    [InlineData("isnull", FilterOperators.IsNull)]
    [InlineData("isnotnull", FilterOperators.IsNotNull)]
    public void Operators_MapToExpectedOperators(string kendoOperator, string expectedOperator)
    {
        var filter = new KendoFilter
        {
            Logic = "and",
            Filters = new List<KendoFilterDescriptor>
            {
                new()
                {
                    Field = "field",
                    Operator = kendoOperator,
                    Value = Element("\"value\"")
                }
            }
        };

        var result = KendoQueryOptionsParser.Parse(new KendoRequest { Filter = filter });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(expectedOperator);
    }

    [Fact]
    public void Pagination_TakeAndSkip_MapsToPageAndPageSize()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Take = 50,
            Skip = 150
        });

        result.Paging.PageSize.Should().Be(50);
        result.Paging.Page.Should().Be(4); // (150 / 50) + 1
    }

    [Fact]
    public void Pagination_PageAndPageSize_MapsToPageAndPageSize()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Page = 3,
            PageSize = 25
        });

        result.Paging.PageSize.Should().Be(25);
        result.Paging.Page.Should().Be(3);
    }

    [Fact]
    public void Groups_MapsGroupFieldsToGroupByPreservingOrder()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Group = new List<KendoGroupDescriptor>
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
    public void Aggregates_MapsToAggregateModels()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Aggregates = new List<KendoAggregateDescriptor>
            {
                new() { Field = "gold", Aggregate = "sum" },
                new() { Field = "silver", Aggregate = "average" }
            }
        });

        result.Aggregates.Should().HaveCount(2);
        result.Aggregates[0].Field.Should().Be("gold");
        result.Aggregates[0].Function.Should().Be("sum");
        result.Aggregates[1].Field.Should().Be("silver");
        result.Aggregates[1].Function.Should().Be("avg");
    }

    [Fact]
    public void ParseJsonRequest_ParsesCompleteKendoRequest()
    {
        var json = """
        {
          "take": 50,
          "skip": 100,
          "filter": {
            "logic": "and",
            "filters": [
              {
                "field": "name",
                "operator": "contains",
                "value": "Peter"
              }
            ]
          },
          "sort": [
            {
              "field": "name",
              "dir": "asc"
            }
          ],
          "group": [
            {
              "field": "country",
              "dir": "asc"
            }
          ],
          "aggregate": [
            {
              "field": "gold",
              "aggregate": "sum"
            }
          ]
        }
        """;

        var result = KendoQueryOptionsParser.Parse(json);

        // Verify pagination
        result.Paging.PageSize.Should().Be(50);
        result.Paging.Page.Should().Be(3); // (100 / 50) + 1

        // Verify filter
        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Field.Should().Be("name");
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);

        // Verify sort
        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("name");
        result.Sort[0].Descending.Should().BeFalse();

        // Verify group
        result.GroupBy.Should().Equal("country");

        // Verify aggregates
        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Field.Should().Be("gold");
        result.Aggregates[0].Function.Should().Be("sum");
    }

    [Fact]
    public void NestedFilter_ParsesComplexLogicStructure()
    {
        var json = """
        {
          "filter": {
            "logic": "and",
            "filters": [
              {
                "logic": "or",
                "filters": [
                  {
                    "field": "status",
                    "operator": "eq",
                    "value": "Open"
                  },
                  {
                    "field": "status",
                    "operator": "eq",
                    "value": "Approved"
                  }
                ]
              },
              {
                "field": "name",
                "operator": "contains",
                "value": "Peter"
              }
            ]
          }
        }
        """;

        var result = KendoQueryOptionsParser.Parse(json);

        result.Filter!.Logic.Should().Be(LogicOperator.And);
        result.Filter.Groups.Should().HaveCount(1);
        result.Filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        result.Filter.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Field.Should().Be("name");
    }

    [Fact]
    public void GroupAggregates_ProcessesAggregatesWithinGroups()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Group = new List<KendoGroupDescriptor>
            {
                new()
                {
                    Field = "country",
                    Aggregates = new List<KendoAggregateDescriptor>
                    {
                        new() { Field = "gold", Aggregate = "sum" },
                        new() { Field = "silver", Aggregate = "count" }
                    }
                }
            }
        });

        result.GroupBy.Should().Equal("country");
        result.Aggregates.Should().HaveCount(2);
        result.Aggregates[0].Field.Should().Be("gold");
        result.Aggregates[0].Function.Should().Be("sum");
        result.Aggregates[1].Field.Should().Be("silver");
        result.Aggregates[1].Function.Should().Be("count");
    }

    [Fact]
    public void ExtensionMethod_ToQueryOptions_ConvertsKendoRequest()
    {
        var request = new KendoRequest
        {
            Filter = new KendoFilter
            {
                Logic = "and",
                Filters = new List<KendoFilterDescriptor>
                {
                    new()
                    {
                        Field = "name",
                        Operator = "contains",
                        Value = Element("\"Peter\"")
                    }
                }
            }
        };

        var result = request.ToQueryOptions();

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Field.Should().Be("name");
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
    }

    [Fact]
    public void ExtensionMethod_FromKendoJson_ParsesJsonString()
    {
        var json = """
        {
          "filter": {
            "logic": "and",
            "filters": [
              {
                "field": "name",
                "operator": "contains",
                "value": "Peter"
              }
            ]
          }
        }
        """;
        var root = JsonDocument.Parse(json).RootElement.Clone();
        var result = root.ToQueryOptions();

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Field.Should().Be("name");
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
    }

    [Fact]
    public void ExtensionMethod_ApplyKendoRequest_ModifiesExistingQueryOptions()
    {
        var existingOptions = new QueryOptions();
        var kendoRequest = new KendoRequest
        {
            Sort = new List<KendoSortDescriptor>
            {
                new() { Field = "name", Dir = "asc" }
            }
        };

        var result = existingOptions.ApplyKendoRequest(kendoRequest);

        result.Sort.Should().HaveCount(1);
        result.Sort[0].Field.Should().Be("name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void AggregateFunction_NormalizesAverageToAvg()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Aggregates = new List<KendoAggregateDescriptor>
            {
                new() { Field = "score", Aggregate = "average" }
            }
        });

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be("avg");
    }

    [Fact]
    public void AggregateAlias_GeneratesCorrectAlias()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Aggregates = new List<KendoAggregateDescriptor>
            {
                new() { Field = "totalScore", Aggregate = "sum" }
            }
        });

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Alias.Should().Be("totalScoreSum");
    }

    [Fact]
    public void AggregateAlias_HandlesNestedFields()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Aggregates = new List<KendoAggregateDescriptor>
            {
                new() { Field = "address.city", Aggregate = "count" }
            }
        });

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Alias.Should().Be("addressCityCount");
    }

    [Fact]
    public void IsNullOperator_HandlesNullValue()
    {
        var result = KendoQueryOptionsParser.Parse(new KendoRequest
        {
            Filter = new KendoFilter
            {
                Logic = "and",
                Filters = new List<KendoFilterDescriptor>
                {
                    new()
                    {
                        Field = "deletedAt",
                        Operator = "isnull",
                        Value = Element("null")
                    }
                }
            }
        });

        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
        result.Filter.Filters[0].Value.Should().BeNull();
    }

    private static JsonElement Element(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }
}
