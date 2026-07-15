using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Parsers.Fql;
using MiniODataApi = FlexQuery.NET.Parsers.MiniOData.MiniOData;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryNonDiSmokeTests
{
    public FlexQueryNonDiSmokeTests()
    {
        FlexQueryCore.Reset();
        FlexQueryEFCore.Reset();
        FlexQueryDapper.Reset();
    }

    [Fact]
    public void Full_setup_pipeline_does_not_throw()
    {
        Action act = () =>
        {
            MiniODataApi.Register();
            FlexQueryCore.Configure(o => o.MaxPageSize = 200);
            Fql.Register(); 
            FlexQueryDapper.Configure(o => o.Model.Entity<TestEntity>());
            FlexQueryEFCore.Setup();
        };

        act.Should().NotThrow();
    }
}
