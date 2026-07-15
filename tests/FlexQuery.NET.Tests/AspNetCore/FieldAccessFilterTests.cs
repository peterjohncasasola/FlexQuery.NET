using FlexQuery.NET.AspNetCore.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlexQuery.NET.Tests.AspNetCore;

public class FieldAccessFilterTests
{
    [Fact]
    public void OnActionExecuting_DoesNotThrow_WithValidContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var filter = new FieldAccessFilter();
        var httpContext = new DefaultHttpContext { RequestServices = sp };

        var routeData = new RouteData();
        routeData.Values["controller"] = "Test";

        var actionDescriptor = new ActionDescriptor();
        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        Action act = () => filter.OnActionExecuting(context);
        act.Should().NotThrow();
    }
}
