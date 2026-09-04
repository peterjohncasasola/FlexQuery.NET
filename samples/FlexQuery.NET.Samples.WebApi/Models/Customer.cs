namespace FlexQuery.NET.Samples.WebApi.Models;

public sealed class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime CreatedDate { get; set; }

    // Relationship navigation property
    public List<Order> Orders { get; set; } = new();
}

public sealed class CustomerDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<Order> Orders { get; set; } = new();
}

/// <summary>
/// Public surface contract DTO: <c>CustomerName</c> maps to <c>Customer.FirstName</c> via
/// <c>MapField</c>; entity fields <c>City</c>, <c>Status</c>, <c>Salary</c>, <c>CreatedDate</c>
/// are intentionally not exposed. Note: no aggregate alias properties (e.g. Total) are
/// required — ungrouped aggregates flow through <c>QueryResult.Aggregates</c>.
/// </summary>
public sealed class CustomerSummaryDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Renamed-navigation DTO: <c>Purchases</c> maps to the entity's <c>Orders</c> collection
/// via MapField, so <c>include=Purchases</c> must resolve through the public surface.
/// </summary>
public sealed class CustomerWithPurchasesDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<Order> Purchases { get; set; } = new();
}

/// <summary>Nested DTO projection target for Orders.</summary>
public sealed class OrderSummaryDto
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Root DTO with a nested DTO-typed navigation for the nested-select scenario:
/// <c>select=customerName,orders(id,orderDate,status)</c> projects each order through
/// <see cref="OrderSummaryDto"/> server-side.
/// </summary>
public sealed class CustomerOrderSummaryDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderSummaryDto> Orders { get; set; } = new();
}
