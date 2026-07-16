using FlexQuery.NET.Security;

namespace FlexQuery.NET.Tests.Security;

public class SafePropertyResolverTests
{

    [Fact]
    public void TryResolveChain_SimplePath_ReturnsChain()
    {
        var found = SafePropertyResolver.TryResolveChain(typeof(Customer), "Name", out var chain);

        found.Should().BeTrue();
        chain.Should().HaveCount(1);
        chain[0].Name.Should().Be("Name");
    }

    [Fact]
    public void TryResolveChain_NestedPath_ReturnsChain()
    {
        var found = SafePropertyResolver.TryResolveChain(typeof(Customer), "Address.City", out var chain);

        found.Should().BeTrue();
        chain.Should().HaveCount(2);
        chain[0].Name.Should().Be("Address");
        chain[1].Name.Should().Be("City");
    }

    [Fact]
    public void TryResolveChain_NonExistentPath_ReturnsFalse()
    {
        var found = SafePropertyResolver.TryResolveChain(typeof(Customer), "NonExistent", out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolveChain_EmptyPath_ReturnsFalse()
    {
        var found = SafePropertyResolver.TryResolveChain(typeof(Customer), "", out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetCollectionElementType_ListType_ReturnsElement()
    {
        var found = SafePropertyResolver.TryGetCollectionElementType(typeof(List<Order>), out var elementType);

        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }

    [Fact]
    public void TryGetCollectionElementType_NonCollection_ReturnsFalse()
    {
        var found = SafePropertyResolver.TryGetCollectionElementType(typeof(string), out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetCollectionElementType_ArrayType_ReturnsElement()
    {
        var found = SafePropertyResolver.TryGetCollectionElementType(typeof(Order[]), out var elementType);

        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }

    [Fact]
    public void TryGetCollectionElementType_IEnumerable_ReturnsElement()
    {
        var found = SafePropertyResolver.TryGetCollectionElementType(typeof(IEnumerable<Order>), out var elementType);

        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }
}
