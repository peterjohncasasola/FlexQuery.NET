using FlexQuery.NET.Parsers;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Tests.DependencyInjection;

/// <summary>
/// Tests for <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions.AddMiniOData"/>.
/// </summary>
public class MiniODataParserServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMiniOData_throws_when_services_is_null()
    {
        IServiceCollection? services = null;

        var act = () => services.AddMiniOData();

        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddMiniOData_registers_IQueryParser()
    {
        var services = new ServiceCollection();

        services.AddMiniOData();

        var descriptors = services.Where(d => d.ServiceType == typeof(IQueryParser)).ToList();
        descriptors.Should().ContainSingle();
        descriptors[0].Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddMiniOData_registers_parser_in_registry()
    {
        var services = new ServiceCollection();
        services.AddMiniOData();

        QueryParserRegistry.IsRegistered(QuerySyntax.MiniOData).Should().BeTrue();
    }
}
