using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryCoreTests
{
    public FlexQueryCoreTests()
    {
        FlexQueryCore.Reset();
    }

    [Fact]
    public void DefaultOptions_WhenNotConfigured_ReturnsEmptyOptions()
    {
        var options = FlexQueryCore.DefaultOptions;

        options.Should().NotBeNull();
    }

    [Fact]
    public void Configure_SetsDefaultOptions()
    {
        FlexQueryCore.Configure(opts => opts.DefaultPageSize = 50);

        FlexQueryCore.DefaultOptions.DefaultPageSize.Should().Be(50);
    }

    [Fact]
    public void Configure_WithoutAction_UsesDefaults()
    {
        FlexQueryCore.Configure();

        FlexQueryCore.DefaultOptions.Should().NotBeNull();
    }

    [Fact]
    public void Configure_Twice_ThrowsInvalidOperationException()
    {
        FlexQueryCore.Configure();

        Action act = () => FlexQueryCore.Configure();

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*already been configured*");
    }

    [Fact]
    public void Reset_AllowsReconfiguration()
    {
        FlexQueryCore.Configure(opts => opts.DefaultPageSize = 10);
        FlexQueryCore.Reset();

        FlexQueryCore.Configure(opts => opts.DefaultPageSize = 25);

        FlexQueryCore.DefaultOptions.DefaultPageSize.Should().Be(25);
    }

    [Fact]
    public void Configure_SetsQuerySyntaxGlobally()
    {
        FlexQueryCore.Configure(opts => opts.DefaultQuerySyntax = QuerySyntax.NativeDsl);

        FlexQueryCore.DefaultOptions.DefaultQuerySyntax.Should().Be(QuerySyntax.NativeDsl);
    }
}
