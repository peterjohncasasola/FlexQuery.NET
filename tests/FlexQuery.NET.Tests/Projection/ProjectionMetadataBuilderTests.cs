using FlexQuery.NET.Models;
using FlexQuery.NET.Projection;

namespace FlexQuery.NET.Tests.Projection;

public class ProjectionMetadataBuilderTests
{
    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Build_ReturnsProjectionMetadata()
    {
        var options = new QueryOptions { Select = ["Id", "Name"] };

        var result = ProjectionMetadataBuilder.Build(typeof(TestEntity), options);

        result.Should().NotBeNull();
        result.EntityType.Should().Be(typeof(TestEntity));
    }

    [Fact]
    public void Build_EmptyOptions_ReturnsMetadata()
    {
        var result = ProjectionMetadataBuilder.Build(typeof(TestEntity), new QueryOptions());

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
}
