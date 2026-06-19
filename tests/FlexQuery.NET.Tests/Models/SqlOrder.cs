namespace FlexQuery.NET.Tests.Models;

public sealed class SqlOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
    public int CustomerId { get; set; }
    public SqlCustomer Customer { get; set; } = null!;
    public List<SqlOrderItem> Items { get; set; } = [];
}
