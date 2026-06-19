namespace FlexQuery.NET.Tests.Models;

public sealed class SqlCustomer
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public SqlAddress? Address { get; set; }
    public List<SqlOrder> Orders { get; set; } = [];
}
