using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Projection;

namespace FlexQuery.NET.Tests.Projection;

public class ProjectionMetadataBuilderTests
{
    private static SelectNode S(string field, string? alias = null, List<SelectNode>? children = null)
    {
        var node = new SelectNode { Field = field, Alias = alias };
        if (children != null) node.Children.AddRange(children);
        return node;
    }

    [Fact]
    public void Build_ReturnsProjectionMetadata()
    {
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" }] };

        var result = ProjectionMetadataBuilder.Build(typeof(Customer), options);

        result.Should().NotBeNull();
        result.EntityType.Should().Be(typeof(Customer));
    }

    [Fact]
    public void Build_EmptyOptions_ReturnsMetadata()
    {
        var result = ProjectionMetadataBuilder.Build(typeof(Customer), new QueryOptions());

        result.Should().NotBeNull();
    }

    [Fact]
    public void IsIEnumerable_CollectionType_ReturnsTrue()
    {
        var result = ProjectionMetadataBuilder.IsIEnumerable(typeof(List<int>), out var itemType);

        result.Should().BeTrue();
        itemType.Should().Be(typeof(int));
    }

    [Fact]
    public void IsIEnumerable_String_ReturnsFalse()
    {
        var result = ProjectionMetadataBuilder.IsIEnumerable(typeof(string), out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsIEnumerable_Int_ReturnsFalse()
    {
        var result = ProjectionMetadataBuilder.IsIEnumerable(typeof(int), out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void Build_NestedAlias_FieldTypesUsesAlias()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile", children: new List<SelectNode>
                    { S("Bio", "ProfileBio") } )
            }
        };

        var result = ProjectionMetadataBuilder.Build(typeof(Customer), options);

        result.Should().NotBeNull();
        result.IsProjected.Should().BeTrue();
        result.FieldTypes.Should().ContainKey("ProfileBio");
        result.FieldTypes["ProfileBio"].Should().Be(typeof(string));
    }
}


