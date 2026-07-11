using FlexQuery.NET.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Parsers;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class AspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AspNetCore_AddFlexQuery_registers_FlexQueryOptions()
    {
        var services = new ServiceCollection();

        services.AddFlexQuery();

        var descriptor = services.Should().ContainSingle(d => d.ServiceType == typeof(FlexQueryOptions)).Subject;
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AspNetCore_AddFlexQuery_registers_IFlexQueryProcessor()
    {
        var services = new ServiceCollection();

        services.AddFlexQuery();

        var descriptor = services.Should().ContainSingle(d => d.ServiceType == typeof(IFlexQueryProcessor)).Subject;
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationType.Should().Be(typeof(FlexQueryProcessor));
    }

    [Fact]
    public void AspNetCore_AddFlexQuery_resolves_FlexQueryOptions()
    {
        var services = new ServiceCollection();
        services.AddFlexQuery();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<FlexQueryOptions>();

        options.Should().NotBeNull();
    }

    [Fact]
    public void AspNetCore_AddFlexQuery_applies_configuration()
    {
        var services = new ServiceCollection();
        const int expectedMaxPageSize = 200;

        services.AddFlexQuery(o => o.MaxPageSize = expectedMaxPageSize);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<FlexQueryOptions>();

        options.MaxPageSize.Should().Be(expectedMaxPageSize);
    }

    [Fact]
    public void AspNetCore_AddFlexQuery_uses_default_options()
    {
        var services = new ServiceCollection();
        services.AddFlexQuery();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<FlexQueryOptions>();

        options.MaxPageSize.Should().Be(1000);
        options.DefaultPageSize.Should().Be(20);
        options.CaseInsensitive.Should().BeTrue();
        options.IncludeTotalCount.Should().BeTrue();
        options.StrictFieldValidation.Should().BeTrue();
        options.MaxFieldDepth.Should().Be(5);
    }

    [Fact]
    public void AspNetCore_AddFlexQuery_returns_services()
    {
        var services = new ServiceCollection();

        var result = services.AddFlexQuery();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AspNetCore_AddFlexQuery_throws_when_services_is_null()
    {
        IServiceCollection? services = null;

        var act = () => services.AddFlexQuery();

        act.Should().ThrowExactly<ArgumentNullException>();
    }
}
