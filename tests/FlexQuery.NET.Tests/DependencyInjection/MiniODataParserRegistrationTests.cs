using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.MiniOData;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class MiniODataParserRegistrationTests
{
    [Fact]
    public void Register_adds_parser_to_registry()
    {
        NET.Parsers.MiniOData.MiniOData.Register();

        QueryParserRegistry.IsRegistered(QuerySyntax.MiniOData).Should().BeTrue();
    }
}
