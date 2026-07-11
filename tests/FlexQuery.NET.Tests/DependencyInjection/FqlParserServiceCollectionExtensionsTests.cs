using FlexQuery.NET.Parsers;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Tests.DependencyInjection;

/// <summary>
/// Tests for <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions.AddFqlParser"/>.
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

    [Fact]
    public void AddFqlParser_registers_IQueryParser()
    {
        var services = new ServiceCollection();

        services.AddFqlParser();

        var descriptors = services.Where(d => d.ServiceType == typeof(IQueryParser)).ToList();
        descriptors.Should().ContainSingle();
        descriptors[0].Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddFqlParser_registers_parser_in_registry()
    {
        var services = new ServiceCollection();
        services.AddFqlParser();

        QueryParserRegistry.IsRegistered(QuerySyntax.Fql).Should().BeTrue();
    }
}
