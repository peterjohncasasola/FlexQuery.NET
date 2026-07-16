namespace FlexQuery.NET.Tests.Shared.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int Age { get; set; }
    public string? City { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SSN { get; set; }
    public decimal Salary { get; set; }
    public string Category { get; set; } = string.Empty;
    public string SecretField { get; set; } = string.Empty;
    
    public string Country { get; set; } = string.Empty;

    public Address? Address { get; set; }
    public List<Address> Addresses { get; set; } = [];
    public Profile? Profile { get; set; }
    public List<Order> Orders { get; set; } = [];
}