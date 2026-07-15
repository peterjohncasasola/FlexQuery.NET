using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Tests.Models;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryDapperConfigurationTests
{
    public FlexQueryDapperConfigurationTests()
    {
        FlexQueryDapper.Reset();
    }

    [Fact]
    public void Configure_stores_global_model()
    {
        FlexQueryDapper.Configure(o => o.Model.Entity<TestEntity>());

        FlexQueryDapper.DefaultModel.Should().NotBeNull();
        FlexQueryDapper.DefaultModel.Should().BeOfType<FlexQueryModel>();
    }

    [Fact]
    public void Configure_applies_entity_mappings()
    {
        FlexQueryDapper.Configure(o =>
        {
            o.Model.Entity<TestEntity>().ToTable("TestEntities");
        });

        FlexQueryDapper.DefaultModel.Should().NotBeNull();
    }

    [Fact]
    public void Configure_NotThrows_When_Configure_Is_Null()
    {
        Action act = () => FlexQueryDapper.Configure(null);

        act.Should().NotThrow();
    }
}
