using DynamicQueryable.Extensions;
using DynamicQueryable.Parsers;
using DynamicQueryable.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace DynamicQueryable.Tests.Tests;

public class GroupByTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    private static global::DynamicQueryable.Models.QueryOptions Parse(Dictionary<string, string> raw)
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
}
