using FlexQuery.NET.Security;

namespace FlexQuery.NET.Tests.Security;

public class FieldRegistryTests
{

    [Fact]
    public void IsAllowed_NoRegistration_ReturnsTrue()
    {
        FieldRegistry.IsAllowed(typeof(Customer), "AnyField").Should().BeTrue();
    }

    [Fact]
    public void RegisterAndIsAllowed_RegisteredField_ReturnsTrue()
    {
        FieldRegistry.Register<Customer>(["Id", "Name"]);

        try
        {
            FieldRegistry.IsAllowed(typeof(Customer), "Id").Should().BeTrue();
            FieldRegistry.IsAllowed(typeof(Customer), "Name").Should().BeTrue();
        }
        finally
        {
            FieldRegistry.Clear<Customer>();
        }
    }

    [Fact]
    public void RegisterAndIsAllowed_UnregisteredField_ReturnsFalse()
    {
        FieldRegistry.Register<Customer>(["Id"]);

        try
        {
            FieldRegistry.IsAllowed(typeof(Customer), "Name").Should().BeFalse();
        }
        finally
        {
            FieldRegistry.Clear<Customer>();
        }
    }

    [Fact]
    public void Clear_RemovesRegistration()
    {
        FieldRegistry.Register<Customer>(["Id"]);
        FieldRegistry.Clear<Customer>();

        FieldRegistry.IsAllowed(typeof(Customer), "Id").Should().BeTrue();
    }

    [Fact]
    public void Register_CaseInsensitive()
    {
        FieldRegistry.Register<Customer>(["id"]);

        try
        {
            FieldRegistry.IsAllowed(typeof(Customer), "ID").Should().BeTrue();
            FieldRegistry.IsAllowed(typeof(Customer), "Id").Should().BeTrue();
        }
        finally
        {
            FieldRegistry.Clear<Customer>();
        }
    }
}
