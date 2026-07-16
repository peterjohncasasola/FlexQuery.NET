using FlexQuery.NET.Resolvers;

namespace FlexQuery.NET.Tests.Resolvers;

public class FieldResolverTests
{

    [Fact]
    public void TryResolveType_SimpleField_ReturnsType()
    {
        var found = FieldResolver.TryResolveType(typeof(Customer), "Name", null, out var resolvedType);

        found.Should().BeTrue();
        resolvedType.Should().Be(typeof(string));
    }

    [Fact]
    public void TryResolveType_NestedField_ReturnsType()
    {
        var found = FieldResolver.TryResolveType(typeof(Customer), "Address.City", null, out var resolvedType);

        found.Should().BeTrue();
        resolvedType.Should().Be(typeof(string));
    }

    [Fact]
    public void TryResolveType_NonExistentField_ReturnsFalse()
    {
        var found = FieldResolver.TryResolveType(typeof(Customer), "NonExistent", null, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolveType_EmptyPath_ReturnsFalse()
    {
        var found = FieldResolver.TryResolveType(typeof(Customer), "", null, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolveType_NullPath_ReturnsFalse()
    {
        var found = FieldResolver.TryResolveType(typeof(Customer), null!, null, out _);

        found.Should().BeFalse();
    }
}
