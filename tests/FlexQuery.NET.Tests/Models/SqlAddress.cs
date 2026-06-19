namespace FlexQuery.NET.Tests.Models;

public sealed class SqlAddress
{
    public int Id { get; set; }
    public string City { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public SqlCustomer Customer { get; set; } = null!;
}
