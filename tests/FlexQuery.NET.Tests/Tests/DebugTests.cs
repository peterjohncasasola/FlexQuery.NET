using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FluentAssertions;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class DebugTests
{
    private class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Order> Orders { get; set; } = new();
    }

    private class Order
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItem> OrderItems { get; set; } = new();
    }

    private class OrderItem
    {
        public int Id { get; set; }
    }

    [Fact]
    public void ToFlexQueryDebug_Should_Generate_Lambda_String()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "query", "id = 101" }
        });

        IQueryable<Customer> query = new List<Customer>().AsQueryable();
        var debug = query.ToFlexQueryDebug(options);

        debug.LinqLambda.Should().NotBeNullOrEmpty();
        debug.LinqLambda.Should().Contain("Id");
    }

    [Fact]
    public void ToFlexQueryDebug_Should_Preserve_Ast()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "query", "id = 1" }
        });

        IQueryable<Customer> query = new List<Customer>().AsQueryable();
        var debug = query.ToFlexQueryDebug(options);

        debug.Ast.Should().NotBeNull();
        debug.Ast!.ToString().Should().Contain("id eq [1]");
    }

    [Fact]
    public void ToFlexQueryDebug_Should_Generate_Expression_Tree_Visualization()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "query", "id = 1" }
        });

        IQueryable<Customer> query = new List<Customer>().AsQueryable();
        var debug = query.ToFlexQueryDebug(options);

        // Visualizer uses NodeType.ToString() which is "Lambda", "Equal", etc.
        debug.ExpressionTree.Should().Contain("Lambda");
        debug.ExpressionTree.Should().Contain("Equal");
    }
}
