namespace FlexQuery.NET.Tests.Shared.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Sku { get; set; }
    public Order? Order { get; set; }
    public int? ProductId { get; set; }
    public string? Description { get; set; } = null;
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Price { get; set; }
}
