using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class DapperServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFlexQueryDapper_registers_FlexQueryModel()
    {
        var services = new ServiceCollection();

        services.AddFlexQueryDapper();

        var descriptor = services.Should().ContainSingle(d => d.ServiceType == typeof(FlexQueryModel)).Subject;
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddFlexQueryDapper_resolves_FlexQueryModel()
    {
        var services = new ServiceCollection();
        services.AddFlexQueryDapper(o => o.Model.Entity<TestEntity>());
        var provider = services.BuildServiceProvider();

        var model = provider.GetRequiredService<FlexQueryModel>();

        model.Should().NotBeNull();
    }

    [Fact]
    public void AddFlexQueryDapper_with_configure_callback_builds_model()
    {
        var services = new ServiceCollection();

        services.AddFlexQueryDapper(o => o.Model.Entity<TestEntity>());
        var provider = services.BuildServiceProvider();

        var model = provider.GetRequiredService<FlexQueryModel>();

        model.Should().NotBeNull();
    }

    [Fact]
    public void AddFlexQueryDapper_without_configure_builds_model()
    {
        var services = new ServiceCollection();

        services.AddFlexQueryDapper();
        var provider = services.BuildServiceProvider();

        var model = provider.GetRequiredService<FlexQueryModel>();

        model.Should().NotBeNull();
    }

    [Fact]
    public void AddFlexQueryDapper_returns_services()
    {
        var services = new ServiceCollection();

        var result = services.AddFlexQueryDapper();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddFlexQueryDapper_throws_when_services_is_null()
    {
        IServiceCollection? services = null;

        var act = () => services.AddFlexQueryDapper();

        act.Should().ThrowExactly<ArgumentNullException>();
    }
}
