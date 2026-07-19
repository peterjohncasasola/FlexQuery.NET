using System;
using System.Collections.Generic;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.OpenApi.Documentation;

internal sealed record FlexQueryDocumentation(string? Description, object? Example);

internal static class FlexQueryDocumentationRegistry
{
    private static readonly Dictionary<Type, FlexQueryDocumentation> Entries = new()
    {
        [typeof(FlexQueryRequest)] = new(SchemaDocumentation.FlexQueryRequest, ExampleProvider.CreateRequestExample()),
        [typeof(FlexQueryParameters)] = new(SchemaDocumentation.FlexQueryParameters, ExampleProvider.CreateParametersExample()),
        [typeof(FilterGroup)] = new(SchemaDocumentation.FilterGroup, null),
        [typeof(FilterCondition)] = new(SchemaDocumentation.FilterCondition, null),
        [typeof(SortNode)] = new(SchemaDocumentation.SortNode, null),
        [typeof(PagingOptions)] = new(SchemaDocumentation.PagingOptions, null),
        [typeof(Aggregate)] = new(SchemaDocumentation.Aggregate, null),
        [typeof(HavingNode)] = new(SchemaDocumentation.HavingNode, null),
        [typeof(IncludeNode)] = new(SchemaDocumentation.IncludeNode, null),
        [typeof(ProjectionMode)] = new(SchemaDocumentation.ProjectionMode, null),
        [typeof(LogicOperator)] = new(SchemaDocumentation.LogicOperator, null),
        [typeof(AggregateFunction)] = new(SchemaDocumentation.AggregateFunction, null)
    };

    public static bool TryGet(Type type, out FlexQueryDocumentation? documentation)
    {
        if (Entries.TryGetValue(type, out documentation!))
            return true;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(QueryResult<>))
        {
            documentation = new FlexQueryDocumentation(SchemaDocumentation.QueryResult, ExampleProvider.CreateQueryResultExample());
            return true;
        }

        documentation = null;
        return false;
    }
}
