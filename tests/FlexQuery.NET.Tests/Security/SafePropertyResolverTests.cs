using FlexQuery.NET.Security;

namespace FlexQuery.NET.Tests.Security;

public class SafePropertyResolverTests
{
    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Address? Address { get; set; }
        public List<Order> Orders { get; set; } = [];
    }

    private sealed class Address
    {
        public string City { get; set; } = string.Empty;
    }

    private sealed class Order
    {
        public int Number { get; set; }
    }

    [Fact]
    public void TryResolveChain_SimplePath_ReturnsChain()
    {
        var found = SafePropertyResolver.TryResolveChain(typeof(TestEntity), "Name", out var chain);

        found.Should().BeTrue();
        chain.Should().HaveCount(1);
        chain[0].Name.Should().Be("Name");
    }

    [Fact]
    public void TryResolveChain_NestedPath_ReturnsChain()
    {
        var found = SafePropertyResolver.TryResolveChain(typeof(TestEntity), "Address.City", out var chain);

        found.Should().BeTrue();
        chain.Should().HaveCount(2);
        chain[0].Name.Should().Be("Address");
        chain[1].Name.Should().Be("City");
    }

    [Fact]
    public void TryResolveChain_NonExistentPath_ReturnsFalse()
    {
        var found = SafePropertyResolver.TryResolveChain(typeof(TestEntity), "NonExistent", out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolveChain_EmptyPath_ReturnsFalse()
    {
        var found = SafePropertyResolver.TryResolveChain(typeof(TestEntity), "", out _);

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
