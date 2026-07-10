using Microsoft.Extensions.DependencyInjection;
using FlexQuery.NET.Parsers.Fql.DependencyInjection;

namespace FlexQuery.NET.Tests.DependencyInjection;

/// <summary>
/// Tests for <see cref="FlexQuery.NET.Parsers.Fql.DependencyInjection.ServiceCollectionExtensions.AddFqlParser"/>.
///
/// Design note: AddFqlParser registers the parser in the internal static
/// <c>QueryParserRegistry</c> rather than in the DI container. There is no public
/// API to verify registration was successful. To make parser registration publicly
/// observable, consider making <c>QueryParserRegistry</c> a public static class
/// (its members—Register, Resolve, IsRegistered—are already public).
/// </summary>
public class FqlParserServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFqlParser_throws_when_services_is_null()
    {
        IServiceCollection? services = null;

        var act = () => services.AddFqlParser();

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("services");
    }
}
