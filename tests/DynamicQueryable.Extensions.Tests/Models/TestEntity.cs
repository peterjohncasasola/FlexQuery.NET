namespace DynamicQueryable.Tests.Models;

/// <summary>Shared test entity used across all test classes.</summary>
public class TestEntity
{
    public int      Id        { get; set; }
    public string   Name      { get; set; } = string.Empty;
    public int      Age       { get; set; }
    public DateTime CreatedAt { get; set; }
    public string   City      { get; set; } = string.Empty;
    public Status   Status    { get; set; }

    public Profile? Profile   { get; set; }
    public List<Order> Orders { get; set; } = [];
}

public class Profile
{
    public int Id { get; set; }
    public string Bio { get; set; } = string.Empty;
}

public class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
}

public enum Status
{
    Active,
    Inactive,
    Pending
}
