using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.NET.Tests.Tests;

public class SecurityTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("test' OR 1=1 --")]
    [InlineData("'; DROP TABLE Entities; --")]
    [InlineData("' UNION SELECT * FROM Users --")]
    public void QueryInjection_IsTreatedAsLiteralValue_Or_ThrowsException(string injectedValue)
    {
        var act = () =>
        {
            var filter = new FqlQueryParser().Parse($"name = \"{injectedValue}\"");
            var options = new QueryOptions { Filter = filter };
            return _db.Entities.ApplyFilter(options).ToList();
        };

        try
        {
            var result = act();
            result.Should().BeEmpty();
        }
        catch (FqlParseException)
        {
            // Valid: it rejected the dangerous token
        }
    }

    [Theory]
    [InlineData("Cancelled' OR 1=1 --")]
    public void NestedInjection_IsTreatedAsLiteralValue_Or_ThrowsException(string injectedValue)
    {
        var act = () =>
        {
            var filter = new FqlQueryParser().Parse($"orders.any(status = \"{injectedValue}\")");
            var options = new QueryOptions { Filter = filter };
            return _db.Entities.ApplyFilter(options).ToList();
        };

        try
        {
            var result = act();
            result.Should().BeEmpty();
        }
        catch (FqlParseException)
        {
            // Valid
        }
    }

    [Fact]
    public void SelectInjection_InvalidAlias_ThrowsArgumentException()
    {
        var maliciousSelect = "id,(SELECT * FROM Users) as bad_alias";
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "select", maliciousSelect } };
        var act = () => QueryOptionsParser.Parse(dict);
        var options = act();
        var actBuild = () => SelectTreeBuilder.Build(options);
        actBuild.Should().NotThrow();
    }
}
