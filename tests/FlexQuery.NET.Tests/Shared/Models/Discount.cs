namespace FlexQuery.NET.Tests.Shared.Models;

public class Discount
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}
