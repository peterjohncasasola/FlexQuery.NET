using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Builders.Fluent;

/// <summary>Builds a list of AggregateModel entries for use with QueryBuilder.Aggregate.</summary>
public sealed class AggregateBuilder
{
    private readonly List<AggregateModel> _aggregates = new();

    internal List<AggregateModel> Build() => _aggregates;

    /// <summary>Adds a SUM(field) AS alias aggregate.</summary>
    public AggregateBuilder Sum(string field, string alias)
    {
        _aggregates.Add(new AggregateModel { Function = AggregateFunction.Sum, Field = field, Alias = alias });
        return this;
    }

    /// <summary>Adds a COUNT(*) AS alias aggregate.</summary>
    public AggregateBuilder Count(string alias)
    {
        _aggregates.Add(new AggregateModel { Function = AggregateFunction.Count, Alias = alias });
        return this;
    }

    /// <summary>Adds a COUNT(field) AS alias aggregate.</summary>
    public AggregateBuilder Count(string field, string alias)
    {
        _aggregates.Add(new AggregateModel { Function = AggregateFunction.Count, Field = field, Alias = alias });
        return this;
    }

    /// <summary>Adds an AVG(field) AS alias aggregate.</summary>
    public AggregateBuilder Avg(string field, string alias)
    {
        _aggregates.Add(new AggregateModel { Function = AggregateFunction.Avg, Field = field, Alias = alias });
        return this;
    }

    /// <summary>Adds a MIN(field) AS alias aggregate.</summary>
    public AggregateBuilder Min(string field, string alias)
    {
        _aggregates.Add(new AggregateModel { Function = AggregateFunction.Min, Field = field, Alias = alias });
        return this;
    }

    /// <summary>Adds a MAX(field) AS alias aggregate.</summary>
    public AggregateBuilder Max(string field, string alias)
    {
        _aggregates.Add(new AggregateModel { Function = AggregateFunction.Max, Field = field, Alias = alias });
        return this;
    }
    
}
