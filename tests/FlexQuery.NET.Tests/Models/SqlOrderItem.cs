namespace FlexQuery.NET.Tests.Models;

public sealed class SqlOrderItem
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public SqlOrder Order { get; set; } = null!;
}
