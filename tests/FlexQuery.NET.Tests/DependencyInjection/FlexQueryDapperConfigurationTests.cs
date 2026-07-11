using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Tests.Models;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryDapperConfigurationTests
{
    [Fact]
    public void BuildModel_returns_model()
    {
        var model = FlexQueryDapper.BuildModel(o => o.Model.Entity<TestEntity>());

        model.Should().NotBeNull();
        model.Should().BeOfType<FlexQueryModel>();
    }

    [Fact]
    public void BuildModel_applies_entity_mappings()
    {
        var model = FlexQueryDapper.BuildModel(o =>
        {
            o.Model.Entity<TestEntity>().ToTable("TestEntities");
        });

        model.Should().NotBeNull();
    }

    [Fact]
    public void BuildModel_throws_when_configure_is_null()
    {
        Action act = () => FlexQueryDapper.BuildModel(null!);

        act.Should().ThrowExactly<ArgumentNullException>();
    }
}
