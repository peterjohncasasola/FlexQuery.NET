using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Tests.Diagnostics;

public class DebugTests
{

    [Fact]
    public void ToFlexQueryDebug_Should_Generate_Lambda_String()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "filter", "id:eq:101" }
        });

        IQueryable<Customer> query = new List<Customer>().AsQueryable();
        var debug = query.ToFlexQueryDebug(options);

        debug.LinqLambda.Should().NotBeNullOrEmpty();
        debug.LinqLambda.Should().Contain("Id");
    }

    [Fact]
    public void ToFlexQueryDebug_Should_Generate_Expression_Tree_Visualization()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "filter", "id:eq:1" }
        });

        IQueryable<Customer> query = new List<Customer>().AsQueryable();
        var debug = query.ToFlexQueryDebug(options);

        // Visualizer uses NodeType.ToString() which is "Lambda", "Equal", etc.
        debug.ExpressionTree.Should().Contain("Lambda");
        debug.ExpressionTree.Should().Contain("Equal");
    }
}
