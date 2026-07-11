using FlexQuery.NET.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class FlexQueryEFCoreConfigurationTests
{
    [Fact]
    public void Setup_does_not_throw()
    {
        Action act = () => FlexQueryEFCore.Setup();

        act.Should().NotThrow();
    }
}
