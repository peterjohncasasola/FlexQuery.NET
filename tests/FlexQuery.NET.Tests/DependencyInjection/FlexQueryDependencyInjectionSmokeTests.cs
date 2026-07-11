using FlexQuery.NET.Configuration;
using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.EntityFrameworkCore.Configuration;
using FlexQuery.NET.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryDependencyInjectionSmokeTests
{
    [Fact]
    public void Full_registration_pipeline_resolves_all_services()
    {
        var services = new ServiceCollection();

        services.AddFlexQuery(o => o.MaxPageSize = 200);
        services.AddFqlParser();
        services.AddMiniOData();
        services.AddFlexQueryDapper(o => o.Model.Entity<global::FlexQuery.NET.Tests.Models.TestEntity>());
        services.AddFlexQueryEntityFrameworkCore(o => o.UseNoTracking = true);

        var provider = services.BuildServiceProvider();

        var processor = provider.GetRequiredService<IFlexQueryProcessor>();
        processor.Should().NotBeNull();

        var options = provider.GetRequiredService<FlexQueryOptions>();
        options.Should().NotBeNull();
        options.MaxPageSize.Should().Be(200);

        var model = provider.GetRequiredService<FlexQueryModel>();
        model.Should().NotBeNull();

        var efOptions = provider.GetRequiredService<FlexQueryEfCoreOptions>();
        efOptions.Should().NotBeNull();
        efOptions.UseNoTracking.Should().BeTrue();
    }
}
