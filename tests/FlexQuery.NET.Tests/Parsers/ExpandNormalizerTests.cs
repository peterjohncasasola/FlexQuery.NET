using FlexQuery.NET.Parsers;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Tests.Parsers;

public class ExpandNormalizerTests
{
    [Fact]
    public void Normalize_SinglePath_ReturnsSingleNode()
    {
        var ast = new List<ExpandAst>
        {
            new() { Path = ["Orders"] }
        };

        var result = ExpandNormalizer.Normalize(ast);

        result.Should().ContainSingle();
        result[0].Path.Should().Be("Orders");
        result[0].Children.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_FlatDottedPath_ReturnsRecursiveTree()
    {
        var ast = new List<ExpandAst>
        {
            new() { Path = ["Orders", "OrderItems"] }
        };

        var result = ExpandNormalizer.Normalize(ast);

        result.Should().ContainSingle();
        result[0].Path.Should().Be("Orders");
        result[0].Children.Should().ContainSingle();
        result[0].Children[0].Path.Should().Be("OrderItems");
        result[0].Children[0].Children.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_NestedChildren_ReturnsRecursiveTree()
    {
        var ast = new List<ExpandAst>
        {
            new()
            {
                Path = ["Orders"],
                Children =
                [
                    new() { Path = ["OrderItems"] }
                ]
            }
        };

        var result = ExpandNormalizer.Normalize(ast);

        result.Should().ContainSingle();
        result[0].Path.Should().Be("Orders");
        result[0].Children.Should().ContainSingle();
        result[0].Children[0].Path.Should().Be("OrderItems");
    }

    [Fact]
    public void Normalize_FlatAndNestedProduceSameResult()
    {
        var flatAst = new List<ExpandAst>
        {
            new() { Path = ["Orders", "OrderItems"] }
        };

        var nestedAst = new List<ExpandAst>
        {
            new()
            {
                Path = ["Orders"],
                Children =
                [
                    new() { Path = ["OrderItems"] }
                ]
            }
        };

        var flatResult = ExpandNormalizer.Normalize(flatAst);
        var nestedResult = ExpandNormalizer.Normalize(nestedAst);

        flatResult.Should().HaveCount(1);
        nestedResult.Should().HaveCount(1);
        flatResult[0].Path.Should().Be(nestedResult[0].Path);
        flatResult[0].Children.Should().HaveCount(1);
        nestedResult[0].Children.Should().HaveCount(1);
        flatResult[0].Children[0].Path.Should().Be(nestedResult[0].Children[0].Path);
    }

    [Fact]
    public void Normalize_AttachesFilterToDeepestNode()
    {
        var ast = new List<ExpandAst>
        {
            new()
            {
                Path = ["Orders", "OrderItems"],
                Filter = new() { Logic = LogicOperator.And }
            }
        };

        var result = ExpandNormalizer.Normalize(ast);

        result[0].Filter.Should().BeNull();
        result[0].Children[0].Filter.Should().NotBeNull();
    }

    [Fact]
    public void Normalize_AttachesSortToDeepestNode()
    {
        var ast = new List<ExpandAst>
        {
            new()
            {
                Path = ["Orders", "OrderItems"],
                Sort = [new() { Field = "Id", Descending = true }]
            }
        };

        var result = ExpandNormalizer.Normalize(ast);

        result[0].Sort.Should().BeNull();
        result[0].Children[0].Sort.Should().NotBeNull();
        result[0].Children[0].Sort![0].Field.Should().Be("Id");
    }
}
