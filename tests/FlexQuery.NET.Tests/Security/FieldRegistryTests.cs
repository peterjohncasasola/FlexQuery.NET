using FlexQuery.NET.Security;

namespace FlexQuery.NET.Tests.Security;

public class FieldRegistryTests
{
    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void IsAllowed_NoRegistration_ReturnsTrue()
    {
        FieldRegistry.IsAllowed(typeof(TestEntity), "AnyField").Should().BeTrue();
    }

    [Fact]
    public void RegisterAndIsAllowed_RegisteredField_ReturnsTrue()
    {
        FieldRegistry.Register<TestEntity>(["Id", "Name"]);

        try
        {
            FieldRegistry.IsAllowed(typeof(TestEntity), "Id").Should().BeTrue();
            FieldRegistry.IsAllowed(typeof(TestEntity), "Name").Should().BeTrue();
        }
        finally
        {
            FieldRegistry.Clear<TestEntity>();
        }
    }

    [Fact]
    public void RegisterAndIsAllowed_UnregisteredField_ReturnsFalse()
    {
        FieldRegistry.Register<TestEntity>(["Id"]);

        try
        {
            FieldRegistry.IsAllowed(typeof(TestEntity), "Name").Should().BeFalse();
        }
        finally
        {
            FieldRegistry.Clear<TestEntity>();
        }
    }

    [Fact]
    public void Clear_RemovesRegistration()
    {
        FieldRegistry.Register<TestEntity>(["Id"]);
        FieldRegistry.Clear<TestEntity>();

        FieldRegistry.IsAllowed(typeof(TestEntity), "Id").Should().BeTrue();
    }

    [Fact]
    public void Register_CaseInsensitive()
    {
        FieldRegistry.Register<TestEntity>(["id"]);

        try
        {
            FieldRegistry.IsAllowed(typeof(TestEntity), "ID").Should().BeTrue();
            FieldRegistry.IsAllowed(typeof(TestEntity), "Id").Should().BeTrue();
        }
        finally
        {
            FieldRegistry.Clear<TestEntity>();
        }
    }
}
