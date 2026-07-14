using FlexQuery.NET.Filters;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Tests.Filters;

public class FilterNormalizerTests
{
    [Fact]
    public void Normalize_Null_ReturnsNull()
    {
        FilterNormalizer.Normalize(null).Should().BeNull();
    }

    [Fact]
    public void Normalize_EmptyGroup_ReturnsEmptyGroup()
    {
        var group = new FilterGroupNode { Logic = LogicOperator.And };

        var result = FilterNormalizer.Normalize(group);

        result.Should().NotBeNull();
        result!.Logic.Should().Be(LogicOperator.And);
        result.Children.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_NormalizesFieldNamesToLower()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "NAME", Operator = "EQ", Value = "Alice" }
            ]
        };

        var result = FilterNormalizer.Normalize(group);

        var condition = result!.Children.Should().ContainSingle().Which.Should().BeOfType<FilterConditionNode>().Subject;
        condition.Field.Should().Be("name");
        condition.Operator.Should().Be("eq");
    }

    [Fact]
    public void Normalize_FlattensMatchingChildGroups()
    {
        var inner = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Age", Operator = "gt", Value = "25" }
            ]
        };
        var outer = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" },
                inner
            ]
        };

        var result = FilterNormalizer.Normalize(outer);

        // Inner AND group should be flattened into outer
        result!.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Normalize_DoesNotFlattenNonMatchingLogicGroups()
    {
        var inner = new FilterGroupNode
        {
            Logic = LogicOperator.Or,
            Children =
            [
                new FilterConditionNode { Field = "Age", Operator = "gt", Value = "25" }
            ]
        };
        var outer = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" },
                inner
            ]
        };

        var result = FilterNormalizer.Normalize(outer);

        // Inner OR group should NOT be flattened
        result!.Children.Should().ContainSingle(c => c is FilterGroupNode);
    }

    [Fact]
    public void Normalize_DoesNotFlattenNegatedGroups()
    {
        var inner = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            IsNegated = true,
            Children =
            [
                new FilterConditionNode { Field = "Age", Operator = "gt", Value = "25" }
            ]
        };
        var outer = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" },
                inner
            ]
        };

        var result = FilterNormalizer.Normalize(outer);

        result!.Children.Should().ContainSingle(c => c is FilterGroupNode);
    }

    [Fact]
    public void Normalize_RemovesEmptyFilters()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "", Operator = "eq", Value = "test" },
                new FilterConditionNode { Field = "  ", Operator = "eq", Value = "test" },
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" }
            ]
        };

        var result = FilterNormalizer.Normalize(group);

        result!.Children.Should().ContainSingle().Which.Should().BeOfType<FilterConditionNode>();
    }

    [Fact]
    public void Normalize_DeduplicatesIdenticalConditions()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" },
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" }
            ]
        };

        var result = FilterNormalizer.Normalize(group);

        result!.Children.Should().ContainSingle();
    }

    [Fact]
    public void GenerateCacheKey_Null_ReturnsEmpty()
    {
        FilterNormalizer.GenerateCacheKey(null).Should().BeEmpty();
    }

    [Fact]
    public void GenerateCacheKey_DeterministicAcrossDifferentOrder()
    {
        var group1 = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" },
                new FilterConditionNode { Field = "Age", Operator = "gt", Value = "25" }
            ]
        };
        var group2 = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Age", Operator = "gt", Value = "25" },
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" }
            ]
        };

        var key1 = FilterNormalizer.GenerateCacheKey(group1);
        var key2 = FilterNormalizer.GenerateCacheKey(group2);

        key1.Should().Be(key2);
    }

    [Fact]
    public void GenerateHash_ProducesConsistentHash()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" }
            ]
        };

        var hash1 = FilterNormalizer.GenerateHash(group);
        var hash2 = FilterNormalizer.GenerateHash(group);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GenerateHash_DifferentFilters_DifferentHashes()
    {
        var group1 = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children = [new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" }]
        };
        var group2 = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children = [new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Bob" }]
        };

        FilterNormalizer.GenerateHash(group1).Should().NotBe(FilterNormalizer.GenerateHash(group2));
    }

    [Fact]
    public void NormalizeOrder_PreservesCase()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "NAME", Operator = "EQ", Value = "Alice" }
            ]
        };

        var result = FilterNormalizer.NormalizeOrder(group);

        var condition = result!.Children.Should().ContainSingle().Which.Should().BeOfType<FilterConditionNode>().Subject;
        condition.Field.Should().Be("NAME");
        condition.Operator.Should().Be("EQ");
    }
}
