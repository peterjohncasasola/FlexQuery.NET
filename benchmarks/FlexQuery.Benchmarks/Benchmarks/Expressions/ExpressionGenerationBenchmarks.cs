using BenchmarkDotNet.Attributes;
using FlexQuery.Benchmarks.Abstractions;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using System.Collections.Generic;

namespace FlexQuery.Benchmarks.Benchmarks.Expressions;

public class ExpressionGenerationBenchmarks : BenchmarkBase
{
    [Benchmark]
    public void FlexQuery_ExpressionGeneration()
    {
        // Measure only the overhead of building the expression from options
        ExpressionBuilder.BuildPredicate<FlexQuery.Benchmarks.Models.User>(new QueryOptions 
        { 
            Filter = new FilterGroupNode 
            { 
                Children = new List<FilterNode> 
                { 
                    new FilterConditionNode { Field = "Status", Operator = "eq", Value = "active" } 
                } 
            }
        });
    }
}
