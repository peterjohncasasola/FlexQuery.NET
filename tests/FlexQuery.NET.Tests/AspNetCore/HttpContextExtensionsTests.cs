using FlexQuery.NET.AspNetCore;
using FlexQuery.NET.Options;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FlexQuery.NET.Tests.AspNetCore;

public class HttpContextExtensionsTests
{
    [Fact]
    public void GetFlexQueryExecutionOptions_NullContext_ReturnsDefault()
    {
        var options = ((HttpContext?)null).GetFlexQueryExecutionOptions();

        options.Should().NotBeNull();
        options.Should().BeOfType<QueryExecutionOptions>();
    }

    [Fact]
    public void GetFlexQueryExecutionOptions_NoItems_ReturnsDefault()
    {
        var context = new DefaultHttpContext();

        var options = context.GetFlexQueryExecutionOptions();

        options.Should().NotBeNull();
        options.Should().BeOfType<QueryExecutionOptions>();
    }

    [Fact]
    public void GetFlexQueryExecutionOptions_StoredOptions_ReturnsStored()
    {
        var context = new DefaultHttpContext();
        var stored = new QueryExecutionOptions { DefaultSortField = "Name" };
        context.Items["ExecutionOptions"] = stored;

        var options = context.GetFlexQueryExecutionOptions();

        options.Should().BeSameAs(stored);
    }

    [Fact]
    public void GetFlexQueryExecutionOptions_LegacyKey_ReturnsLegacy()
    {
        var context = new DefaultHttpContext();
        var legacy = new QueryExecutionOptions { DefaultSortField = "Id" };
        context.Items["FlexQueryExecutionOptions"] = legacy;

        var options = context.GetFlexQueryExecutionOptions();

        options.Should().BeSameAs(legacy);
    }

    [Fact]
    public void GetFlexQueryExecutionOptions_NewerKeyPreferredOverLegacy()
    {
        var context = new DefaultHttpContext();
        var legacy = new QueryExecutionOptions { DefaultSortField = "Id" };
        var current = new QueryExecutionOptions { DefaultSortField = "Name" };
        context.Items["FlexQueryExecutionOptions"] = legacy;
        context.Items["ExecutionOptions"] = current;

        var options = context.GetFlexQueryExecutionOptions();

        options.Should().BeSameAs(current);
    }
}
