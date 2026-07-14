using FlexQuery.NET.Caching;
using System.Collections.Concurrent;
using FlexQuery.NET.Builders;

namespace FlexQuery.NET.Tests.Builders;

public class DynamicTypeBuilderTests
{
    [Fact]
    public void GetDynamicType_Returns_Type_With_Requested_Properties()
    {
        var props = new Dictionary<string, Type>
        {
            ["Id"] = typeof(int),
            ["Name"] = typeof(string),
            ["Total"] = typeof(decimal)
        };

        var type = DynamicTypeBuilder.GetDynamicType(props);

        type.Should().NotBeNull();
        type.GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Name", "Total");
        type.GetProperty("Id")!.PropertyType.Should().Be(typeof(int));
        type.GetProperty("Name")!.PropertyType.Should().Be(typeof(string));
        type.GetProperty("Total")!.PropertyType.Should().Be(typeof(decimal));
    }

    [Fact]
    public void GetDynamicType_Returns_Same_Type_For_Same_Properties()
    {
        var props = new Dictionary<string, Type>
        {
            ["Id"] = typeof(int),
            ["Name"] = typeof(string),
        };

        var type1 = DynamicTypeBuilder.GetDynamicType(props);
        var type2 = DynamicTypeBuilder.GetDynamicType(props);

        type1.Should().BeSameAs(type2);
    }

    [Fact]
    public void GetDynamicType_Returns_Different_Types_For_Different_Properties()
    {
        var props1 = new Dictionary<string, Type> { ["Id"] = typeof(int) };
        var props2 = new Dictionary<string, Type> { ["Name"] = typeof(string) };

        var type1 = DynamicTypeBuilder.GetDynamicType(props1);
        var type2 = DynamicTypeBuilder.GetDynamicType(props2);

        type1.Should().NotBeSameAs(type2);
    }

    [Fact]
    public void GetDynamicType_Properties_Are_Readable_And_Writable()
    {
        var props = new Dictionary<string, Type>
        {
            ["Value"] = typeof(int),
        };

        var type = DynamicTypeBuilder.GetDynamicType(props);
        var instance = Activator.CreateInstance(type)!;

        type.GetProperty("Value")!.SetValue(instance, 42);
        var result = type.GetProperty("Value")!.GetValue(instance);

        result.Should().Be(42);
    }

    [Fact]
    public void GetDynamicType_Returns_Same_For_Same_Properties_Different_Order()
    {
        var props1 = new Dictionary<string, Type>
        {
            ["A"] = typeof(int),
            ["B"] = typeof(string),
        };

        var props2 = new Dictionary<string, Type>
        {
            ["B"] = typeof(string),
            ["A"] = typeof(int),
        };

        var type1 = DynamicTypeBuilder.GetDynamicType(props1);
        var type2 = DynamicTypeBuilder.GetDynamicType(props2);

        type1.Should().BeSameAs(type2);
    }

    // ── Cache Eviction ──────────────────────────────────────────────────

    [Fact]
    public void GetDynamicType_Caches_Up_To_MaxCacheSize()
    {
        var oldMax = FlexQueryCacheSettings.MaxCacheSize;
        try
        {
            FlexQueryCacheSettings.MaxCacheSize = 5;

            for (int i = 0; i < 10; i++)
            {
                var props = new Dictionary<string, Type>
                {
                    [$"Prop{i}"] = typeof(int),
                };
                DynamicTypeBuilder.GetDynamicType(props);
            }

            DynamicTypeBuilder.Count.Should().BeLessOrEqualTo(5);
        }
        finally
        {
            FlexQueryCacheSettings.MaxCacheSize = oldMax;
            DynamicTypeBuilder.Clear();
        }
    }

    [Fact]
    public void GetDynamicType_Evicts_Oldest_Type_When_Cache_Is_Full()
    {
        // DynamicTypeBuilder uses FIFO eviction: when the cache exceeds MaxCacheSize,
        // the oldest entry (first inserted) is removed.
        var oldMax = FlexQueryCacheSettings.MaxCacheSize;
        try
        {
            FlexQueryCacheSettings.MaxCacheSize = 2;

            var typeA = DynamicTypeBuilder.GetDynamicType(new() { ["A"] = typeof(int) });
            var typeB = DynamicTypeBuilder.GetDynamicType(new() { ["B"] = typeof(string) });

            // typeC insertion pushes cache over limit → typeA evicted
            var typeC = DynamicTypeBuilder.GetDynamicType(new() { ["C"] = typeof(decimal) });

            DynamicTypeBuilder.Count.Should().Be(2);

            // Request typeA again — it was evicted, so a new type is created
            var typeA_again = DynamicTypeBuilder.GetDynamicType(new() { ["A"] = typeof(int) });

            typeA_again.Should().NotBeSameAs(typeA);
            typeA_again.GetProperties().Should().ContainSingle(p => p.Name == "A");
            typeA_again.GetProperty("A")!.PropertyType.Should().Be(typeof(int));
            DynamicTypeBuilder.Count.Should().Be(2);
        }
        finally
        {
            FlexQueryCacheSettings.MaxCacheSize = oldMax;
            DynamicTypeBuilder.Clear();
        }
    }

    // ── Cache Reuse ─────────────────────────────────────────────────────

    [Fact]
    public void GetDynamicType_Reuses_Cached_Type_For_Identical_Shape()
    {
        var props = new Dictionary<string, Type> { ["Id"] = typeof(int) };
        var first = DynamicTypeBuilder.GetDynamicType(props);
        var initialCount = DynamicTypeBuilder.Count;

        for (int i = 0; i < 100; i++)
        {
            var result = DynamicTypeBuilder.GetDynamicType(props);
            result.Should().BeSameAs(first);
        }

        DynamicTypeBuilder.Count.Should().Be(initialCount);
    }

    // ── Thread Safety ───────────────────────────────────────────────────

    [Fact]
    public void GetDynamicType_Concurrent_SameShape_Returns_Same_Type()
    {
        var results = new ConcurrentBag<Type>();
        var props = new Dictionary<string, Type> { ["Id"] = typeof(int) };

        // Prime the cache
        var baseline = DynamicTypeBuilder.GetDynamicType(props);

        Parallel.For(0, 50, _ =>
        {
            var t = DynamicTypeBuilder.GetDynamicType(props);
            results.Add(t);
        });

        results.Should().AllSatisfy(t => t.Should().BeSameAs(baseline));
    }

    [Fact]
    public void GetDynamicType_Concurrent_DifferentShapes_DoesNotCorruptCache()
    {
        var exceptions = new ConcurrentBag<Exception>();
        var oldMax = FlexQueryCacheSettings.MaxCacheSize;
        try
        {
            FlexQueryCacheSettings.MaxCacheSize = 20;

            Parallel.For(0, 100, i =>
            {
                try
                {
                    var props = new Dictionary<string, Type>
                    {
                        [$"Field{i}"] = typeof(int),
                    };
                    var type = DynamicTypeBuilder.GetDynamicType(props);
                    if (type == null)
                        throw new InvalidOperationException("GetDynamicType returned null");
                    var prop = type.GetProperty($"Field{i}");
                    if (prop == null)
                        throw new InvalidOperationException($"Missing property Field{i}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            exceptions.Should().BeEmpty();
            DynamicTypeBuilder.Count.Should().BeLessOrEqualTo(20);
        }
        finally
        {
            FlexQueryCacheSettings.MaxCacheSize = oldMax;
            DynamicTypeBuilder.Clear();
        }
    }

    // ── Edge Cases ──────────────────────────────────────────────────────

    [Fact]
    public void GetDynamicType_Empty_Properties_Returns_Type()
    {
        // Empty shapes are intentionally supported. This is used when a query
        // selects no fields, requiring a type with only a parameterless constructor
        // (e.g., count-only queries or aggregation-only results).
        var props = new Dictionary<string, Type>();

        var type = DynamicTypeBuilder.GetDynamicType(props);

        type.Should().NotBeNull();
        type.GetProperties().Should().BeEmpty();
    }

    [Fact]
    public void GetDynamicType_NullProperties_ThrowsArgumentNullException()
    {
        var act = () => DynamicTypeBuilder.GetDynamicType(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetDynamicType_Has_Parameterless_Constructor()
    {
        var props = new Dictionary<string, Type>
        {
            ["Id"] = typeof(int),
        };

        var type = DynamicTypeBuilder.GetDynamicType(props);
        var constructor = type.GetConstructor(Type.EmptyTypes);

        constructor.Should().NotBeNull();
        constructor!.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Clear_Removes_All_Cached_Types()
    {
        var props = new Dictionary<string, Type> { ["Id"] = typeof(int) };
        DynamicTypeBuilder.GetDynamicType(props);

        DynamicTypeBuilder.Clear();

        DynamicTypeBuilder.Count.Should().Be(0);
    }
}
