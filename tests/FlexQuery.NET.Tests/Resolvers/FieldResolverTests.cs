using FlexQuery.NET.Resolvers;

namespace FlexQuery.NET.Tests.Resolvers;

public class FieldResolverTests
{
    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Address? Address { get; set; }
    }

    private sealed class Address
    {
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    [Fact]
    public void TryResolveType_SimpleField_ReturnsType()
    {
        var found = FieldResolver.TryResolveType(typeof(TestEntity), "Name", null, out var resolvedType);

        found.Should().BeTrue();
        resolvedType.Should().Be(typeof(string));
    }

    [Fact]
    public void TryResolveType_NestedField_ReturnsType()
    {
        var found = FieldResolver.TryResolveType(typeof(TestEntity), "Address.City", null, out var resolvedType);

        found.Should().BeTrue();
        resolvedType.Should().Be(typeof(string));
    }

    [Fact]
    public void TryResolveType_NonExistentField_ReturnsFalse()
    {
        var found = FieldResolver.TryResolveType(typeof(TestEntity), "NonExistent", null, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolveType_EmptyPath_ReturnsFalse()
    {
        var found = FieldResolver.TryResolveType(typeof(TestEntity), "", null, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolveType_NullPath_ReturnsFalse()
    {
        var found = FieldResolver.TryResolveType(typeof(TestEntity), null!, null, out _);

        found.Should().BeFalse();
    }
}
