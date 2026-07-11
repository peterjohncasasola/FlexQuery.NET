using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FqlParserRegistrationTests
{
    [Fact]
    public void Register_adds_parser_to_registry()
    {
        Fql.Register();

        QueryParserRegistry.IsRegistered(QuerySyntax.Fql).Should().BeTrue();
    }
}
