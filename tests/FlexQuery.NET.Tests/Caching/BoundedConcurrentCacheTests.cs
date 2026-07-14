using FlexQuery.NET.Caching;

namespace FlexQuery.NET.Tests.Caching;

public class BoundedConcurrentCacheTests
{
    [Fact]
    public void GetOrAdd_NewKey_AddsAndReturns()
    {
        var cache = new BoundedConcurrentCache<string, int>();

        var value = cache.GetOrAdd("key", _ => 42);

        value.Should().Be(42);
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void GetOrAdd_ExistingKey_ReturnsExisting()
    {
        var cache = new BoundedConcurrentCache<string, int>();
        cache.GetOrAdd("key", _ => 42);

        var value = cache.GetOrAdd("key", _ => 99);

        value.Should().Be(42);
    }

    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrue()
    {
        var cache = new BoundedConcurrentCache<string, int>();
        cache.GetOrAdd("key", _ => 42);

        var found = cache.TryGetValue("key", out var value);

        found.Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void TryGetValue_MissingKey_ReturnsFalse()
    {
        var cache = new BoundedConcurrentCache<string, int>();

        var found = cache.TryGetValue("nonexistent", out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void Set_AddsNewValue()
    {
        var cache = new BoundedConcurrentCache<string, int>();

        cache.Set("key", 42);

        cache.TryGetValue("key", out var value).Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        var cache = new BoundedConcurrentCache<string, int>();
        cache.Set("key", 42);

        cache.Set("key", 99);

        cache.TryGetValue("key", out var value).Should().BeTrue();
        value.Should().Be(99);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new BoundedConcurrentCache<string, int>();
        cache.GetOrAdd("key1", _ => 1);
        cache.GetOrAdd("key2", _ => 2);

        cache.Clear();

        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Clear_AfterClear_Reusable()
    {
        var cache = new BoundedConcurrentCache<string, int>();
        cache.GetOrAdd("key", _ => 42);
        cache.Clear();

        cache.GetOrAdd("key", _ => 99).Should().Be(99);
    }

    [Fact]
    public void Count_StartsAtZero()
    {
        var cache = new BoundedConcurrentCache<string, int>();

        cache.Count.Should().Be(0);
    }
}
