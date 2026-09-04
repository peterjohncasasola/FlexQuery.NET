using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Parsers;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers;

/// <summary>
/// Deep expansion normalization: flat dotted expand paths are merged into a
/// hierarchical IncludeNode tree; options stay scoped to their own path; duplicate
/// full paths are rejected deterministically at parse time.
/// </summary>
public class ExpandNormalizerTests
{
    [Fact]
    public void MultipleFlatPaths_MergeIntoHierarchicalTree()
    {
        var asts = DslExpandParser.Parse("Orders(take=3;filter=Status:eq:Delivered;sort=Id:desc),Orders.OrderItems(take=5)");
        var tree = ExpandNormalizer.Normalize(asts);

        // One root: Orders (merged from both flat paths).
        tree.Should().ContainSingle();
        var orders = tree[0];
        orders.Path.Should().Be("Orders");
        orders.Take.Should().Be(3);
        orders.Filter.Should().NotBeNull();
        orders.Filter!.Filters.Should().Contain(f => f.Field == "Status" && f.Value == "Delivered");
        orders.Sort.Should().Contain(s => s.Field == "Id" && s.Descending);

        // Child: OrderItems with its own scoped options.
        var orderItems = orders.Children.Should().ContainSingle().Subject;
        orderItems.Path.Should().Be("OrderItems");
        orderItems.Take.Should().Be(5);
        orderItems.Filter.Should().BeNull();
        orderItems.Sort.Should().BeNull();
    }

    [Fact]
    public void ThreeLevelPath_NormalizesIntoRecursiveChain()
    {
        var asts = DslExpandParser.Parse("A.B.C(take=2)");
        var tree = ExpandNormalizer.Normalize(asts);

        var a = tree[0];
        a.Path.Should().Be("A");
        a.Take.Should().BeNull(); // options attach to the deepest segment

        var b = a.Children.Should().ContainSingle().Subject;
        b.Path.Should().Be("B");
        b.Take.Should().BeNull();

        var c = b.Children.Should().ContainSingle().Subject;
        c.Path.Should().Be("C");
        c.Take.Should().Be(2);
    }

    [Fact]
    public void DuplicatePath_AtParseTime_Rejected()
    {
        var act = () => DslExpandParser.Parse("Orders(take=3),Orders(take=10)");

        act.Should().Throw<Exception>().WithMessage("*Duplicate expand path*");
    }

    [Fact]
    public void CaseInsensitiveDuplicatePath_Rejected()
    {
        var act = () => DslExpandParser.Parse("Orders(take=3),orders(take=10)");

        act.Should().Throw<Exception>().WithMessage("*Duplicate expand path*");
    }

    [Fact]
    public void SiblingRoots_RemainSeparate()
    {
        var asts = DslExpandParser.Parse("Orders(take=3),Invoices(take=5)");
        var tree = ExpandNormalizer.Normalize(asts);

        tree.Should().HaveCount(2);
        tree[0].Path.Should().Be("Orders");
        tree[0].Take.Should().Be(3);
        tree[1].Path.Should().Be("Invoices");
        tree[1].Take.Should().Be(5);
    }
}
