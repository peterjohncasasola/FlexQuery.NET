using System.Diagnostics;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Models;
using Xunit;

namespace FlexQuery.NET.Tests.Diagnostics;

/// <summary>
/// Verifies <see cref="ConsoleExecutionListener"/> writes every lifecycle event to the
/// console without throwing, including the exception paths.
/// </summary>
public class ConsoleExecutionListenerTests
{
    [Fact]
    public void AllEvents_WriteToConsole_WithoutThrowing()
    {
        var listener = new ConsoleExecutionListener();
        var original = Console.Out;
        var buffer = new StringWriter();
        Console.SetOut(buffer);
        try
        {
            var id = Guid.NewGuid();
            var act = () =>
            {
                listener.QueryParsedAsync(new QueryParsedEvent(id, new QueryOptions(), TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow), CancellationToken.None).GetAwaiter().GetResult();
                listener.QueryTranslatedAsync(new QueryTranslatedEvent(id, "SELECT 1", [new QueryParameter("p0", 42)], TimeSpan.FromMilliseconds(2), DateTimeOffset.UtcNow), CancellationToken.None).GetAwaiter().GetResult();
                listener.QueryExecutedAsync(new QueryExecutedEvent(id, 5, null, TimeSpan.FromMilliseconds(3), DateTimeOffset.UtcNow), CancellationToken.None).GetAwaiter().GetResult();
                listener.QueryMaterializedAsync(new QueryMaterializedEvent(id, new QueryResult<object>(), null, TimeSpan.FromMilliseconds(4), DateTimeOffset.UtcNow), CancellationToken.None).GetAwaiter().GetResult();
            };
            act.Should().NotThrow();
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = buffer.ToString();
        output.Should().Contain("Parsed");
        output.Should().Contain("Translated");
        output.Should().Contain("SQL: SELECT 1");
        output.Should().Contain("p0 = 42");
        output.Should().Contain("rows=5");
        output.Should().Contain("Materialized");
    }

    [Fact]
    public void ExecutedWithException_WritesErrorMessage()
    {
        var listener = new ConsoleExecutionListener();
        var original = Console.Out;
        var buffer = new StringWriter();
        Console.SetOut(buffer);
        try
        {
            var id = Guid.NewGuid();
            listener.QueryExecutedAsync(
                new QueryExecutedEvent(id, null, new InvalidOperationException("kaboom"), TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow),
                CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Console.SetOut(original);
        }

        buffer.ToString().Should().Contain("kaboom");
    }

    [Fact]
    public void MaterializedWithException_WritesErrorMessage()
    {
        var listener = new ConsoleExecutionListener();
        var original = Console.Out;
        var buffer = new StringWriter();
        Console.SetOut(buffer);
        try
        {
            var id = Guid.NewGuid();
            listener.QueryMaterializedAsync(
                new QueryMaterializedEvent(id, null, new Exception("materialize-fail"), TimeSpan.FromMilliseconds(1), DateTimeOffset.UtcNow),
                CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Console.SetOut(original);
        }

        buffer.ToString().Should().Contain("materialize-fail");
    }
}
