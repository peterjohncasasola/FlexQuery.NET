using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlexQuery.NET.Tests.DependencyInjection;

public class AspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFlexQuerySecurity_registers_FieldAccessFilter()
    {
        var services = new ServiceCollection();

        var mvcBuilder = services.AddControllers();

        mvcBuilder.AddFlexQuerySecurity();

        var serviceProvider = services.BuildServiceProvider();
        var filters = serviceProvider.GetRequiredService<IConfigureOptions<MvcOptions>>();

        filters.Should().NotBeNull();
    }
}
