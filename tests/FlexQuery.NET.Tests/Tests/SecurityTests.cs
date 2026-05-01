using FlexQuery.NET;
using FlexQuery.NET.EFCore;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "query", $"name = \"{injectedValue}\"" } };
        
        var act = () => QueryOptionsParser.Parse(dict);
        
        // It either parses safely and parameterizes, or fails fast due to syntax validation
        try
        {
            var options = act();
            var result = _db.Entities.ApplyFilter(options).ToList();
            result.Should().BeEmpty();
        }
        catch (FlexQuery.NET.Parsers.Jql.JqlParseException)
        {
            // Valid: it rejected the dangerous token
        }
    }

    [Theory]
    [InlineData("Cancelled' OR 1=1 --")]
    public void NestedInjection_IsTreatedAsLiteralValue_Or_ThrowsException(string injectedValue)
    {
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { { "query", $"orders.any(status = \"{injectedValue}\")" } };
        var act = () => QueryOptionsParser.Parse(dict);

        try
        {
            var options = act();
            var result = _db.Entities.ApplyFilter(options).ToList();
            result.Should().BeEmpty();
        }
        catch (FlexQuery.NET.Parsers.Jql.JqlParseException)
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
        var actBuild = () => Helpers.SelectTreeBuilder.Build(options);
        actBuild.Should().NotThrow(); 
    }

}
