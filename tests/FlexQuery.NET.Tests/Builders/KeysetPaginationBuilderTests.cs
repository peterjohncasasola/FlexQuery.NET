using System.Linq.Expressions;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Tests.Builders;

public class KeysetPaginationBuilderTests
{
    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    [Fact]
    public void BuildOrderingInfos_EmptySorts_Throws()
    {
        var sorts = new List<SortNode>();

        Action act = () => KeysetPaginationBuilder.BuildOrderingInfos<TestEntity>(sorts);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Keyset pagination requires at least one sort field*");
    }

    [Fact]
    public void BuildOrderingInfos_WithSorts_ReturnsOrderings()
    {
        var sorts = new List<SortNode>
        {
            new() { Field = "Id", Descending = false },
            new() { Field = "Name", Descending = true }
        };

        var orderings = KeysetPaginationBuilder.BuildOrderingInfos<TestEntity>(sorts);

        orderings.Should().HaveCount(2);
        orderings[0].Descending.Should().BeFalse();
        orderings[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void BuildSeekPredicate_WithValues_ProducesValidExpression()
    {
        var sorts = new List<SortNode> { new() { Field = "Id", Descending = false } };
        var orderings = KeysetPaginationBuilder.BuildOrderingInfos<TestEntity>(sorts);
        var cursor = new KeysetCursor(5);

        var predicate = KeysetPaginationBuilder.BuildSeekPredicate<TestEntity>(orderings, cursor.Values);

        predicate.Should().NotBeNull();
        predicate.Parameters.Should().ContainSingle(p => p.Type == typeof(TestEntity));
    }

    [Fact]
    public void BuildSeekPredicate_EmptyOrderings_Throws()
    {
        var orderings = new List<(LambdaExpression, bool)>();
        var cursor = new KeysetCursor(1);

        Action act = () => KeysetPaginationBuilder.BuildSeekPredicate<TestEntity>(orderings, cursor.Values);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Keyset pagination requires at least one sort field*");
    }

    [Fact]
    public void KeysetCursor_ConstructsWithValues()
    {
        var cursor = new KeysetCursor(1, "test", null);

        cursor.Values.Should().HaveCount(3);
        cursor.Values[0].Should().Be(1);
        cursor.Values[1].Should().Be("test");
        cursor.Values[2].Should().BeNull();
    }

    [Fact]
    public void KeysetCursor_Empty_Constructs()
    {
        var cursor = new KeysetCursor();

        cursor.Values.Should().BeEmpty();
    }
}
