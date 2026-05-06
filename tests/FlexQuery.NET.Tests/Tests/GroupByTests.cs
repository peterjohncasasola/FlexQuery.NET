using FlexQuery.NET;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Tests;

public class GroupByTests : IDisposable
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
    public async Task GroupBy_SelectAggregates_Having_AppliesServerTranslatableShape()
    {
        var options = Parse(new()
        {
            ["group"] = "CustomerId",
            ["select"] = "CustomerId,sum(Total),count(Id)",
            ["having"] = "sum(Total):gt:100"
        });

        var query = _db.Orders.AsQueryable().ApplySelect(options);
        var sql = query.ToQueryString();
        sql.ToUpperInvariant().Should().Contain("GROUP BY");
        sql.ToUpperInvariant().Should().Contain("HAVING");

        var rows = await query.ToListAsync();
        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task GroupBy_LinqStyleAggregates_Works()
    {
        var options = Parse(new()
        {
            ["group"] = "CustomerId",
            ["select"] = "CustomerId,Total.sum(),Id.count()",
        });

        options.Aggregates.Should().HaveCount(2, "it should have parsed two aggregates from the select string");
        options.Aggregates[0].Function.Should().Be("sum");
        options.Aggregates[0].Field.Should().Be("Total");
        options.Aggregates[0].Alias.Should().Be("SUM_Total");
        options.Aggregates[1].Function.Should().Be("count");
        options.Aggregates[1].Field.Should().Be("Id");
        options.Aggregates[1].Alias.Should().Be("COUNT_Id");

        var query = _db.Orders.AsQueryable().ApplySelect(options);
        var rows = await query.ToListAsync();
        rows.Should().NotBeEmpty();
    }
}
