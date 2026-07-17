using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Builders.Fluent;

/// <summary>Builds a list of AggregateModel entries for use with QueryBuilder.Aggregate.</summary>
public sealed class AggregateBuilder
{
    private readonly List<AggregateModel> _aggregates = new();

    internal List<AggregateModel> Build() => _aggregates;

    /// <summary>Adds a SUM(field) aggregate with an optional alias.</summary>
    public AggregateBuilder Sum(string field, string? alias)
    {
        _aggregates.Add(new AggregateModel
        {
            Function = AggregateFunction.Sum,
            Field = field,
            Alias = alias is null || alias == "" ? ParserUtilities.BuildAggregateAlias("sum", field) : alias
        });
        return this;
    }

    /// <summary>Adds a COUNT(field) aggregate with an optional alias.</summary>
    public AggregateBuilder Count(string field, string? alias)
    {
        _aggregates.Add(new AggregateModel
        {
            Function = AggregateFunction.Count,
            Field = field,
            Alias = alias is null || alias == "" ? ParserUtilities.BuildAggregateAlias("count", field) : alias
        });
        return this;
    }

    /// <summary>Adds an AVG(field) aggregate with an optional alias.</summary>
    public AggregateBuilder Avg(string field, string? alias)
    {
        _aggregates.Add(new AggregateModel
        {
            Function = AggregateFunction.Avg,
            Field = field,
            Alias = alias is null || alias == "" ? ParserUtilities.BuildAggregateAlias("avg", field) : alias
        });
        return this;
    }

    /// <summary>Adds a MIN(field) aggregate with an optional alias.</summary>
    public AggregateBuilder Min(string field, string? alias)
    {
        _aggregates.Add(new AggregateModel
        {
            Function = AggregateFunction.Min,
            Field = field,
            Alias = alias is null || alias == "" ? ParserUtilities.BuildAggregateAlias("min", field) : alias
        });
        return this;
    }

    /// <summary>Adds a MAX(field) aggregate with an optional alias.</summary>
    public AggregateBuilder Max(string field, string? alias)
    {
        _aggregates.Add(new AggregateModel
        {
            Function = AggregateFunction.Max,
            Field = field,
            Alias = alias is null || alias == "" ? ParserUtilities.BuildAggregateAlias("max", field) : alias
        });
        return this;
    }
    
}
