using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers;

public class QueryParserRegistryTests
{
    [Fact]
    public void IsRegistered_NativeDsl_IsTrue()
    {
        QueryParserRegistry.IsRegistered(QuerySyntax.NativeDsl).Should().BeTrue();
    }

    [Fact]
    public void Resolve_NativeDsl_ReturnsParser()
    {
        var parser = QueryParserRegistry.Resolve(QuerySyntax.NativeDsl);

        parser.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_UnregisteredSyntax_Throws()
    {
        var unregistered = new[] { QuerySyntax.Fql, QuerySyntax.MiniOData }
            .Where(s => !QueryParserRegistry.IsRegistered(s))
            .ToList();

        unregistered.Should().NotBeEmpty("at least one syntax should be unregistered by default");

        foreach (var syntax in unregistered)
        {
            var act = () => QueryParserRegistry.Resolve(syntax);
            act.Should().Throw<ParserNotRegisteredException>();
        }
    }

    [Fact]
    public void Register_AddsParser_AndResolveReturnsIt()
    {
        var dummy = new DslQueryParser();
        QueryParserRegistry.Register(QuerySyntax.MiniOData, dummy);

        QueryParserRegistry.IsRegistered(QuerySyntax.MiniOData).Should().BeTrue();
        QueryParserRegistry.Resolve(QuerySyntax.MiniOData).Should().BeSameAs(dummy);
    }

    [Fact]
    public void Register_Duplicate_OverwritesPrevious()
    {
        var first = new DslQueryParser();
        var second = new DslQueryParser();
        QueryParserRegistry.Register(QuerySyntax.MiniOData, first);
        QueryParserRegistry.Register(QuerySyntax.MiniOData, second);

        QueryParserRegistry.Resolve(QuerySyntax.MiniOData).Should().BeSameAs(second);
    }
}
