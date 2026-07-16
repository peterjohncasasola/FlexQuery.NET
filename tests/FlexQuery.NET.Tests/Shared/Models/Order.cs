namespace FlexQuery.NET.Tests.Shared.Models;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public List<OrderItem> OrderItems { get; set; } = [];
}
