using FlexQuery.NET.EntityFrameworkCore.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class EntityFrameworkCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFlexQueryEntityFrameworkCore_registers_FlexQueryEfCoreOptions()
    {
        var services = new ServiceCollection();

        services.AddFlexQueryEntityFrameworkCore();

        var descriptor = services.Should().ContainSingle(d => d.ServiceType == typeof(FlexQueryEfCoreOptions)).Subject;
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddFlexQueryEntityFrameworkCore_resolves_FlexQueryEfCoreOptions()
    {
        var services = new ServiceCollection();
        services.AddFlexQueryEntityFrameworkCore();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<FlexQueryEfCoreOptions>();

        options.Should().NotBeNull();
    }

    [Fact]
    public void AddFlexQueryEntityFrameworkCore_applies_configuration()
    {
        var services = new ServiceCollection();

        services.AddFlexQueryEntityFrameworkCore(o => o.UseNoTracking = true);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<FlexQueryEfCoreOptions>();

        options.UseNoTracking.Should().BeTrue();
    }

    [Fact]
    public void AddFlexQueryEntityFrameworkCore_uses_defaults()
    {
        var services = new ServiceCollection();

        services.AddFlexQueryEntityFrameworkCore();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<FlexQueryEfCoreOptions>();

        options.UseNoTracking.Should().BeNull();
        options.UseSplitQuery.Should().BeNull();
        options.IgnoreAutoIncludes.Should().BeNull();
        options.IgnoreQueryFilters.Should().BeNull();
    }

    [Fact]
    public void AddFlexQueryEntityFrameworkCore_returns_services()
    {
        var services = new ServiceCollection();

        var result = services.AddFlexQueryEntityFrameworkCore();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddFlexQueryEntityFrameworkCore_throws_when_services_is_null()
    {
        IServiceCollection? services = null;

        var act = () => services.AddFlexQueryEntityFrameworkCore();

        act.Should().ThrowExactly<ArgumentNullException>();
    }
}
