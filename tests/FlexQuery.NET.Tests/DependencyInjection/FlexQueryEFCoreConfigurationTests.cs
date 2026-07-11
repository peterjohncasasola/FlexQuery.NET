using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.EntityFrameworkCore.Configuration;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryEFCoreConfigurationTests
{
    [Fact]
    public void Setup_Does_Not_Throw()
    {
        Action act = () => FlexQueryEFCore.Setup();

        act.Should().NotThrow();
    }

    [Fact]
    public void Configure_Stores_Global_Options()
    {
        FlexQueryEFCore.Configure(null);

        FlexQueryEFCore.DefaultOptions.Should().NotBeNull();
    }

    [Fact]
    public void Configure_Without_Delegate_Sets_Default_Options()
    {
        FlexQueryEFCore.Configure(null);

        FlexQueryEFCore.DefaultOptions.Should().NotBeNull();
    }
}
