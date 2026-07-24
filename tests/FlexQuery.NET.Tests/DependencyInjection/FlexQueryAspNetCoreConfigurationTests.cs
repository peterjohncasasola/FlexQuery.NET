using FlexQuery.NET.Configuration;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryAspNetCoreConfigurationTests
{
    public FlexQueryAspNetCoreConfigurationTests()
    {
        FlexQueryCore.Reset();
    }

    [Fact]
    public void Configure_returns_FlexQueryOptions()
    {
        FlexQueryCore.Configure();
        var options = FlexQueryCore.DefaultOptions;
        options.Should().NotBeNull();
    }

    [Fact]
    public void Configure_applies_configuration()
    {
        const int expectedMaxPageSize = 500;
        FlexQueryCore.Configure(o => o.MaxPageSize = expectedMaxPageSize);
        
        var options = FlexQueryCore.DefaultOptions;
        
        options.MaxPageSize.Should().Be(expectedMaxPageSize);
    }

    [Fact]
    public void Configure_uses_default_options()
    {
        FlexQueryCore.Configure();
        var options =  FlexQueryCore.DefaultOptions;

        options.MaxPageSize.Should().Be(1000);
        options.DefaultPageSize.Should().Be(20);
        options.IncludeTotalCount.Should().BeTrue();
        options.StrictFieldValidation.Should().BeTrue();
        options.MaxFieldDepth.Should().Be(5);
    }
}
