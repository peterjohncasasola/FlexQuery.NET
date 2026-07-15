using FlexQuery.NET.AspNetCore.Filters;
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
        var mvcOptions = serviceProvider.GetRequiredService<IOptions<MvcOptions>>().Value;

        mvcOptions.Filters
            .Any(f => f is TypeFilterAttribute t && t.ImplementationType == typeof(FieldAccessFilter))
            .Should().BeTrue();
    }
}
