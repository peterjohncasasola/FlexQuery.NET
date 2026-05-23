using System.Reflection;
using FlexQuery.NET.AspNetCore.Attributes;
using FlexQuery.NET.AspNetCore.Extensions;
using FlexQuery.NET.AspNetCore.Filters;
using FlexQuery.NET.EFCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class AspNetCoreIntegrationTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void GetFlexQueryExecutionOptions_ShouldReturnDefaultOptions_WhenContextIsNull()
    {
        // Arrange & Act
        var result = HttpContextExtensions.GetFlexQueryExecutionOptions(null!);

        // Assert
        result.Should().NotBeNull();
        result.AllowedFields.Should().BeNull();
        result.BlockedFields.Should().BeNull();
    }

    [Fact]
    public void GetFlexQueryExecutionOptions_ShouldReturnDefaultOptions_WhenNotSetInHttpContext()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var result = context.GetFlexQueryExecutionOptions();

        // Assert
        result.Should().NotBeNull();
        result.AllowedFields.Should().BeNull();
        result.BlockedFields.Should().BeNull();
    }

    [Fact]
    public void GetFlexQueryExecutionOptions_ShouldReturnPopulatedOptions_WhenSetInHttpContext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var options = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };
        context.Items["FlexQueryExecutionOptions"] = options;

        // Act
        var result = context.GetFlexQueryExecutionOptions();

        // Assert
        result.Should().BeSameAs(options);
        result.AllowedFields.Should().Contain("Id").And.Contain("Name");
    }

    [Fact]
    public void FieldAccessFilter_ShouldPopulateOptionsFromActionAttribute()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.TestActionWithAllowed));
        
        var controllerActionDescriptor = new ControllerActionDescriptor
        {
            MethodInfo = methodInfo!,
            ControllerTypeInfo = typeof(TestController).GetTypeInfo()
        };

        var actionContext = new ActionContext(context, new RouteData(), controllerActionDescriptor);
        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new TestController()
        );

        var filter = new FieldAccessFilter();

        // Act
        filter.OnActionExecuting(executingContext);

        // Assert
        var options = context.GetFlexQueryExecutionOptions();
        options.Should().NotBeNull();
        options.AllowedFields.Should().Contain("Name").And.Contain("Email");
        options.MaxFieldDepth.Should().Be(3);
        options.BlockedFields.Should().BeNull();
    }

    [Fact]
    public void FieldAccessFilter_ShouldMergeOptionsFromActionAttribute()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var existingOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Password" }
        };
        context.Items["FlexQueryExecutionOptions"] = existingOptions;

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.TestActionWithAllowed));
        var controllerActionDescriptor = new ControllerActionDescriptor
        {
            MethodInfo = methodInfo!,
            ControllerTypeInfo = typeof(TestController).GetTypeInfo()
        };

        var actionContext = new ActionContext(context, new RouteData(), controllerActionDescriptor);
        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new TestController()
        );

        var filter = new FieldAccessFilter();

        // Act
        filter.OnActionExecuting(executingContext);

        // Assert
        var options = context.GetFlexQueryExecutionOptions();
        options.Should().NotBeNull();
        options.AllowedFields.Should().Contain("Id").And.Contain("Name").And.Contain("Email");
        options.BlockedFields.Should().Contain("Password");
        options.MaxFieldDepth.Should().Be(3);
    }

    [Fact]
    public async Task FlexQueryAsync_WithHttpContext_ShouldEnforceSecurityRulesSuccessfully()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var options = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };
        context.Items["FlexQueryExecutionOptions"] = options;

        var parameters = new FlexQueryParameters
        {
            Filter = "Name:eq:Alice"
        };

        // Act
        var result = await _db.Customers.FlexQueryAsync(parameters, context);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task FlexQueryAsync_WithHttpContext_ShouldThrowValidationExceptionWhenRulesViolated()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var options = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" } // "Name" is not allowed
        };
        context.Items["FlexQueryExecutionOptions"] = options;

        var parameters = new FlexQueryParameters
        {
            Filter = "Name:eq:Alice"
        };

        // Act
        Func<Task> act = async () => await _db.Customers.FlexQueryAsync(parameters, context);

        // Assert
        await act.Should().ThrowAsync<QueryValidationException>()
            .Where(e => e.Result.Errors.Any(err => err.Code == "FIELD_ACCESS_DENIED"));
    }

    public class TestController
    {
        [FieldAccess(Allowed = new[] { "Name", "Email" }, MaxDepth = 3)]
        public void TestActionWithAllowed() { }

        [FieldAccess(Blocked = new[] { "Password" })]
        public void TestActionWithBlocked() { }
    }
}
