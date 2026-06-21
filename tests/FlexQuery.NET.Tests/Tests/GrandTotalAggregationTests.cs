using FlexQuery.NET;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Tests.Fixtures;
using FlexQuery.NET.EFCore;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Tests;

public class GrandTotalAggregationTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    private static global::FlexQuery.NET.Models.QueryOptions Parse(Dictionary<string, string> raw)
    {
        var kvps = raw.Select(kv =>
            new KeyValuePair<string, StringValues>(kv.Key, new StringValues(kv.Value)));
        return QueryOptionsParser.Parse(kvps);
    }

    [Fact]
    public async Task GrandTotals_CountSumMinMaxAvg_WorksForEFCore()
    {
        var options = Parse(new()
        {
            ["select"] = "sum(Total),count(Id),min(Total),max(Total),avg(Total)"
        });

        // Act
        var result = await _db.Orders.AsQueryable().FlexQueryAsync(options);

        // Assert
        result.Data.Should().HaveCount(4); // Normal rows are returned
        result.Aggregates.Should().NotBeNull();
        
        result.Aggregates!.Should().ContainKey("Total");
        var totalAggs = result.Aggregates["Total"];
        totalAggs.Should().ContainKey("sum");
        totalAggs.Should().ContainKey("min");
        totalAggs.Should().ContainKey("max");
        totalAggs.Should().ContainKey("avg");
        
        result.Aggregates!.Should().ContainKey("Id");
        var idAggs = result.Aggregates["Id"];
        idAggs.Should().ContainKey("count");

        // Double check conversion to double in SQLite translation
        Convert.ToDouble(totalAggs["sum"]).Should().Be(279.5);
        Convert.ToDouble(totalAggs["min"]).Should().Be(10.0);
        Convert.ToDouble(totalAggs["max"]).Should().Be(125.5);
        Convert.ToDouble(totalAggs["avg"]).Should().Be(69.875);
        Convert.ToInt32(idAggs["count"]).Should().Be(4);
    }

    [Fact]
    public void GrandTotals_CountSumMinMaxAvg_WorksForQueryable()
    {
        var options = Parse(new()
        {
            ["select"] = "sum(Total),count(Id),min(Total),max(Total),avg(Total)"
        });

        // Act
        var result = _db.Orders.ToList().AsQueryable().FlexQuery(options);

        // Assert
        result.Data.Should().HaveCount(4);
        result.Aggregates.Should().NotBeNull();
        
        result.Aggregates!.Should().ContainKey("Total");
        var totalAggs = result.Aggregates["Total"];
        
        result.Aggregates!.Should().ContainKey("Id");
        var idAggs = result.Aggregates["Id"];

        Convert.ToDouble(totalAggs["sum"]).Should().Be(279.5);
        Convert.ToDouble(totalAggs["min"]).Should().Be(10.0);
        Convert.ToDouble(totalAggs["max"]).Should().Be(125.5);
        Convert.ToDouble(totalAggs["avg"]).Should().Be(69.875);
        Convert.ToInt32(idAggs["count"]).Should().Be(4);
    }
}
