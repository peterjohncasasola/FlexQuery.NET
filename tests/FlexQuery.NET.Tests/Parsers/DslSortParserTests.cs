using FlexQuery.NET.Parsers;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers;

/// <summary>
/// Sort parser regression tests for the space-separated direction form
/// (<c>field ASC</c> / <c>field DESC</c>) alongside the colon form
/// (<c>field:asc</c> / <c>field:desc</c>).
/// </summary>
public class DslSortParserTests
{
    [Theory]
    [InlineData("CustomerFullName:asc", "CustomerFullName", false)]
    [InlineData("CustomerFullName:desc", "CustomerFullName", true)]
    [InlineData("CustomerFullName ASC", "CustomerFullName", false)]
    [InlineData("CustomerFullName DESC", "CustomerFullName", true)]
    [InlineData("CustomerFullName desc", "CustomerFullName", true)]
    [InlineData("CustomerFullName", "CustomerFullName", false)]
    public void Parse_SingleField_AllForms(string raw, string expectedField, bool expectedDesc)
    {
        var nodes = DslSortParser.Parse(raw);

        nodes.Should().ContainSingle();
        nodes[0].Field.Should().Be(expectedField);
        nodes[0].Descending.Should().Be(expectedDesc);
    }

    [Fact]
    public void Parse_MultipleItems_SpaceDirection()
    {
        var nodes = DslSortParser.Parse("Name ASC, Age DESC");

        nodes.Should().HaveCount(2);
        nodes[0].Field.Should().Be("Name");
        nodes[0].Descending.Should().BeFalse();
        nodes[1].Field.Should().Be("Age");
        nodes[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_InvalidDirection_Throws()
    {
        var act = () => DslSortParser.Parse("Name sideways");

        act.Should().Throw<Exception>().WithMessage("*sideways*");
    }
}
