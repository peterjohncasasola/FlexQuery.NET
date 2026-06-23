using FlexQuery.NET.Caching;
using FluentAssertions;
using System.Collections.Concurrent;
using System.Reflection;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class ReflectionCacheTests
{
    private class Customer
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public Address? Address { get; set; }
        public List<Order>? Orders { get; set; }
        public ICollection<Order>? OrderCollection { get; set; }
    }

    private class Address
    {
        public int Id { get; set; }
        public string? Street { get; set; }
        public string? City { get; set; }
    }

    private class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public List<OrderItem>? Items { get; set; }
    }

    private class OrderItem
    {
        public int Id { get; set; }
        public string? ProductName { get; set; }
        public decimal Price { get; set; }
    }

    // ── GetProperty ─────────────────────────────────────────────────────

    [Fact]
    public void GetProperty_Returns_PropertyInfo()
    {
        var prop = ReflectionCache.GetProperty(typeof(Customer), "Name");
        prop.Should().NotBeNull();
        prop!.Name.Should().Be("Name");
        prop.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void GetProperty_Returns_Same_Instance_On_Repeated_Calls()
    {
        var prop1 = ReflectionCache.GetProperty(typeof(Customer), "Name");
        var prop2 = ReflectionCache.GetProperty(typeof(Customer), "Name");
        prop1.Should().BeSameAs(prop2);
    }

    [Fact]
    public void GetProperty_Case_Insensitive_Lookup()
    {
        var prop = ReflectionCache.GetProperty(typeof(Customer), "name");
        prop.Should().NotBeNull();
        prop!.Name.Should().Be("Name");
    }

    [Fact]
    public void GetProperty_Returns_Null_For_Nonexistent_Property()
    {
        var prop = ReflectionCache.GetProperty(typeof(Customer), "NonExistent");
        prop.Should().BeNull();
    }

    [Fact]
    public void GetProperty_Repeated_Miss_Returns_Null_Consistently()
    {
        var prop1 = ReflectionCache.GetProperty(typeof(Customer), "DoesNotExist");
        var prop2 = ReflectionCache.GetProperty(typeof(Customer), "DoesNotExist");
        var prop3 = ReflectionCache.GetProperty(typeof(Customer), "DoesNotExist");

        prop1.Should().BeNull();
        prop2.Should().BeNull();
        prop3.Should().BeNull();
    }

    [Fact]
    public void GetProperty_Empty_String_Returns_Null()
    {
        var result = ReflectionCache.GetProperty(typeof(Customer), "");
        result.Should().BeNull();
    }

    // ── GetProperties ───────────────────────────────────────────────────

    [Fact]
    public void GetProperties_Returns_All_Public_Instance_Properties()
    {
        var props = ReflectionCache.GetProperties(typeof(Address));
        props.Select(p => p.Name).Should().BeEquivalentTo("Id", "Street", "City");
    }

    [Fact]
    public void GetProperties_Cached_Result_Matches()
    {
        var props1 = ReflectionCache.GetProperties(typeof(Customer));
        var props2 = ReflectionCache.GetProperties(typeof(Customer));
        props1.Should().Equal(props2);
    }

    // ── TryResolvePropertyChain ─────────────────────────────────────────

    [Fact]
    public void TryResolvePropertyChain_Simple_Path()
    {
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Name", out var chain);
        found.Should().BeTrue();
        chain.Should().HaveCount(1);
        chain[0].Name.Should().Be("Name");
    }

    [Fact]
    public void TryResolvePropertyChain_Nested_Path()
    {
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Address.Street", out var chain);
        found.Should().BeTrue();
        chain.Should().HaveCount(2);
        chain[0].Name.Should().Be("Address");
        chain[1].Name.Should().Be("Street");
    }

    [Fact]
    public void TryResolvePropertyChain_Collection_Navigation()
    {
        // Confirms collection element traversal:
        // Customer.Orders (List<Order>) resolves element type Order,
        // then continues to Order.Total
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Orders.Total", out var chain);
        found.Should().BeTrue();
        chain.Should().HaveCount(2);
        chain[0].Name.Should().Be("Orders");
        chain[0].PropertyType.Should().Be(typeof(List<Order>));
        chain[1].Name.Should().Be("Total");
        chain[1].PropertyType.Should().Be(typeof(decimal));
    }

    [Fact]
    public void TryResolvePropertyChain_Deep_Nested_Chain()
    {
        // Customer.Orders.Items.ProductName resolves through two collection layers:
        // Customer → List<Order> → Order → List<OrderItem> → OrderItem
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Orders.Items.ProductName", out var chain);
        found.Should().BeTrue();
        chain.Should().HaveCount(3);
        chain[0].Name.Should().Be("Orders");
        chain[1].Name.Should().Be("Items");
        chain[2].Name.Should().Be("ProductName");
    }

    [Fact]
    public void TryResolvePropertyChain_Returns_False_For_Invalid_Path()
    {
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "NonExistent.Property", out var chain);
        found.Should().BeFalse();
        chain.Should().BeEmpty();
    }

    [Fact]
    public void TryResolvePropertyChain_Repeated_Miss_Returns_Consistently()
    {
        var found1 = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Foo.Bar.Baz", out var chain1);
        var found2 = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Foo.Bar.Baz", out var chain2);
        var found3 = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Foo.Bar.Baz", out var chain3);

        found1.Should().BeFalse();
        found2.Should().BeFalse();
        found3.Should().BeFalse();
        chain1.Should().BeEmpty();
        chain2.Should().BeEmpty();
        chain3.Should().BeEmpty();
    }

    [Fact]
    public void TryResolvePropertyChain_Cached_Result_Matches()
    {
        ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Address.City", out var chain1);
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Address.City", out var chain2);
        found.Should().BeTrue();
        chain2.Should().HaveCount(2);
        chain2.Should().Equal(chain1);
    }

    [Fact]
    public void TryResolvePropertyChain_Empty_String_Returns_False()
    {
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "", out var chain);
        found.Should().BeFalse();
        chain.Should().BeEmpty();
    }

    [Fact]
    public void TryResolvePropertyChain_Whitespace_Returns_False()
    {
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "  ", out var chain);
        found.Should().BeFalse();
        chain.Should().BeEmpty();
    }

    [Fact]
    public void TryResolvePropertyChain_LeadingDot_ResolvesRelative()
    {
        // ".Name" → segments: ["Name"] after trim/empty removal
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), ".Name", out var chain);
        found.Should().BeTrue();
        chain.Should().HaveCount(1);
        chain[0].Name.Should().Be("Name");
    }

    [Fact]
    public void TryResolvePropertyChain_TrailingDot_IgnoresTrailingEmptySegment()
    {
        // "Name." → segments: ["Name"] after RemoveEmptyEntries
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Name.", out var chain);
        found.Should().BeTrue();
        chain.Should().HaveCount(1);
        chain[0].Name.Should().Be("Name");
    }

    [Fact]
    public void TryResolvePropertyChain_ConsecutiveDots_MidChainFails()
    {
        // "Name..Address" → segments: ["Name", "Address"] after RemoveEmptyEntries
        // Name resolves to typeof(string), which has no Address property → path fails
        var found = ReflectionCache.TryResolvePropertyChain(typeof(Customer), "Name..Address", out var chain);
        found.Should().BeFalse();
        chain.Should().BeEmpty();
    }

    // ── TryGetCollectionElementType: Standard Types ─────────────────────

    [Fact]
    public void TryGetCollectionElementType_List_Returns_Element_Type()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(List<Order>), out var elementType);
        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }

    [Fact]
    public void TryGetCollectionElementType_Array_Returns_Element_Type()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(Order[]), out var elementType);
        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }

    [Fact]
    public void TryGetCollectionElementType_HashSet_Returns_Element_Type()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(HashSet<Order>), out var elementType);
        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }

    // ── TryGetCollectionElementType: Collection Interfaces ──────────────

    [Fact]
    public void TryGetCollectionElementType_IEnumerable_Returns_Element_Type()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(IEnumerable<Order>), out var elementType);
        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }

    [Fact]
    public void TryGetCollectionElementType_ICollection_Returns_Element_Type()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(ICollection<Order>), out var elementType);
        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }

    [Fact]
    public void TryGetCollectionElementType_IList_Returns_Element_Type()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(IList<Order>), out var elementType);
        found.Should().BeTrue();
        elementType.Should().Be(typeof(Order));
    }

    [Fact]
    public void TryGetCollectionElementType_ValueType_Array_Returns_Element_Type()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(int[]), out var elementType);
        found.Should().BeTrue();
        elementType.Should().Be(typeof(int));
    }

    [Fact]
    public void TryGetCollectionElementType_ValueType_List_Returns_Element_Type()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(List<decimal>), out var elementType);
        found.Should().BeTrue();
        elementType.Should().Be(typeof(decimal));
    }

    // ── TryGetCollectionElementType: Edge Cases ─────────────────────────

    [Fact]
    public void TryGetCollectionElementType_String_Returns_False()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(string), out var elementType);
        found.Should().BeFalse();
        elementType.Should().BeNull();
    }

    [Fact]
    public void TryGetCollectionElementType_Non_Collection_Returns_False()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(int), out var elementType);
        found.Should().BeFalse();
        elementType.Should().BeNull();
    }

    [Fact]
    public void TryGetCollectionElementType_Object_Returns_False()
    {
        var found = ReflectionCache.TryGetCollectionElementType(typeof(object), out var elementType);
        found.Should().BeFalse();
        elementType.Should().BeNull();
    }

    [Fact]
    public void TryGetCollectionElementType_Repeated_Cache_Miss_Consistent()
    {
        var found1 = ReflectionCache.TryGetCollectionElementType(typeof(decimal), out var et1);
        var found2 = ReflectionCache.TryGetCollectionElementType(typeof(decimal), out var et2);

        found1.Should().BeFalse();
        found2.Should().BeFalse();
        et1.Should().BeNull();
        et2.Should().BeNull();
    }

    // ── Thread Safety ───────────────────────────────────────────────────

    [Fact]
    public void Parallel_Access_No_Exceptions_Or_Corruption()
    {
        var exceptions = new ConcurrentBag<Exception>();
        var types = new[] { typeof(Customer), typeof(Address), typeof(Order), typeof(OrderItem) };
        var propertyNames = new[] { "Id", "Name", "Street", "City", "Total", "ProductName", "Price" };

        Parallel.For(0, 100, _ =>
        {
            try
            {
                foreach (var type in types)
                {
                    var props = ReflectionCache.GetProperties(type);
                    if (props == null)
                        throw new InvalidOperationException("GetProperties returned null");

                    foreach (var name in propertyNames)
                    {
                        var prop = ReflectionCache.GetProperty(type, name);
                        if (prop != null && !prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException($"Expected {name} but got {prop.Name}");

                        ReflectionCache.TryResolvePropertyChain(type, "Id", out var chainResult);
                        ReflectionCache.TryGetCollectionElementType(typeof(List<Order>), out var colResult);
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();
    }

    [Fact]
    public void Parallel_Chain_Resolution_No_Exceptions()
    {
        var exceptions = new ConcurrentBag<Exception>();
        var paths = new[] { "Orders.Total", "Address.City", "Orders.Items.Price", "Name" };

        Parallel.For(0, 50, _ =>
        {
            try
            {
                foreach (var path in paths)
                {
                    if (!ReflectionCache.TryResolvePropertyChain(typeof(Customer), path, out var chain))
                        throw new InvalidOperationException($"Failed to resolve path: {path}");

                    if (chain.Count == 0)
                        throw new InvalidOperationException($"Empty chain for path: {path}");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();
    }

    [Fact]
    public void Parallel_CollectionElementCache_Concurrent_Miss_And_Hit()
    {
        var exceptions = new ConcurrentBag<Exception>();
        var types = new[]
        {
            typeof(IEnumerable<string>),
            typeof(ICollection<int>),
            typeof(IList<long>),
            typeof(HashSet<decimal>),
            typeof(List<Order>),
            typeof(Order[]),
            typeof(string),
            typeof(int),
        };

        Parallel.For(0, 100, _ =>
        {
            try
            {
                foreach (var type in types)
                {
                    var found = ReflectionCache.TryGetCollectionElementType(type, out var elementType);
                    if (type == typeof(string) || type == typeof(int))
                    {
                        if (found || elementType != null)
                            throw new InvalidOperationException($"Expected false/null for {type.Name}");
                    }
                    else
                    {
                        if (!found || elementType == null)
                            throw new InvalidOperationException($"Expected element type for {type.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();
    }
}
