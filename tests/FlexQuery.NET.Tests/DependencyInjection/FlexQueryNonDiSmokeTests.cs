using FlexQuery.NET.AspNetCore;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Parsers.Fql;
using FlexQuery.NET.Parsers.MiniOData;
using FQConfig = FlexQuery.NET.Configuration.FlexQueryConfiguration;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryNonDiSmokeTests
{
    [Fact]
    public void Full_setup_pipeline_does_not_throw()
    {
        Action act = () =>
        {
            FQConfig.Configure(o => o.MaxPageSize = 200);
            FqlParser.Register();
            MiniODataParser.Register();
            FlexQueryDapper.BuildModel(o => o.Model.Entity<TestEntity>());
            FlexQueryEFCore.Setup();
        };

        act.Should().NotThrow();
    }
}
