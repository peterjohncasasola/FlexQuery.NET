using System.Text.Json.Serialization;

namespace FlexQuery.NET.Samples.WebApi.Models;

public sealed class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;

    // Relationship navigation property
    [JsonIgnore] // Avoid circular reference issues in JSON serialization
    public Customer? Customer { get; set; }
}
