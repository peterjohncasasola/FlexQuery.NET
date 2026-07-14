using System.Reflection;
using FlexQuery.NET.AspNetCore.Attributes;
using FlexQuery.NET.AspNetCore.Filters;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace FlexQuery.NET.Tests.AspNetCore;

public class FieldAccessFilterFunctionalTests
{
    [FieldAccess(Allowed = new[] { "Id" }, Filterable = new[] { "Name" })]
    private class AttributedController
    {
        [FieldAccess(Allowed = new[] { "Name" }, Blocked = new[] { "Secret" }, DefaultSortField = "Name", DefaultSortDirection = "desc", MaxDepth = 3)]
        public void ActionWithAttribute() { }

        public void ActionWithoutAttribute() { }
    }

    private class PlainController
    {
        public void Action() { }
    }

    private static ActionExecutingContext BuildContext(System.Reflection.MemberInfo? method, System.Reflection.MemberInfo? controller, HttpContext? httpContext = null)
    {
        httpContext ??= new DefaultHttpContext();
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = (controller as System.Type)?.GetTypeInfo() ?? typeof(AttributedController).GetTypeInfo(),
            MethodInfo = (method as System.Reflection.MethodInfo) ?? typeof(AttributedController).GetMethod("ActionWithoutAttribute")!,
            ControllerName = "Attributed",
            ActionName = "Action"
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static QueryExecutionOptions? OptionsFrom(ActionExecutingContext context)
        => context.HttpContext.Items[ContextKeys.ExecutionOptions] as QueryExecutionOptions;

    [Fact]
    public void OnActionExecuting_AppliesActionAttributeToExecutionOptions()
    {
        var method = typeof(AttributedController).GetMethod("ActionWithAttribute")!;
        var ctx = BuildContext(method, typeof(AttributedController));

        new FieldAccessFilter().OnActionExecuting(ctx);

        var options = OptionsFrom(ctx);
        options.Should().NotBeNull();
        options!.AllowedFields.Should().Contain(new[] { "Name" });
        options.BlockedFields.Should().Contain(new[] { "Secret" });
        options.DefaultSortField.Should().Be("Name");
        options.DefaultSortDescending.Should().BeTrue();
        options.MaxFieldDepth.Should().Be(3);
    }

    [Fact]
    public void OnActionExecuting_NoAttribute_StoresNothing()
    {
        var ctx = BuildContext(typeof(PlainController).GetMethod("Action")!, typeof(PlainController));

        new FieldAccessFilter().OnActionExecuting(ctx);

        ctx.HttpContext.Items.Keys.Should().NotContain(ContextKeys.ExecutionOptions);
    }

    [Fact]
    public void OnActionExecuting_ActionAttributeOverridesControllerAttribute()
    {
        var method = typeof(AttributedController).GetMethod("ActionWithAttribute")!;
        var ctx = BuildContext(method, typeof(AttributedController));

        new FieldAccessFilter().OnActionExecuting(ctx);

        var options = OptionsFrom(ctx);
        // Action attribute Allowed=["Name"] wins over controller Allowed=["Id"].
        options!.AllowedFields.Should().BeEquivalentTo(new[] { "Name" });
    }

    [Fact]
    public void OnActionExecuting_MergesWithExistingOptions()
    {
        var method = typeof(AttributedController).GetMethod("ActionWithAttribute")!;
        var httpContext = new DefaultHttpContext();
        var existing = new QueryExecutionOptions { AllowedFields = new() { "X" } };
        httpContext.Items[ContextKeys.ExecutionOptions] = existing;

        var ctx = BuildContext(method, typeof(AttributedController), httpContext);

        new FieldAccessFilter().OnActionExecuting(ctx);

        var options = OptionsFrom(ctx);
        options!.AllowedFields.Should().Contain("X");
        options.AllowedFields.Should().Contain("Name");
    }

    [Fact]
    public void OnActionExecuting_DefaultSortDirectionCaseInsensitive()
    {
        var method = typeof(AttributedController).GetMethod("ActionWithAttribute")!;
        var ctx = BuildContext(method, typeof(AttributedController));

        new FieldAccessFilter().OnActionExecuting(ctx);

        OptionsFrom(ctx)!.DefaultSortDescending.Should().BeTrue();
    }

    [Fact]
    public void FieldAccessAttribute_Defaults()
    {
        var attribute = new FieldAccessAttribute();

        attribute.MaxDepth.Should().Be(-1);
        attribute.Allowed.Should().BeNull();
        attribute.Blocked.Should().BeNull();
        attribute.Filterable.Should().BeNull();
        attribute.Sortable.Should().BeNull();
    }
}
