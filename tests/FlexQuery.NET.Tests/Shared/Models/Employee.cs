namespace FlexQuery.NET.Tests.Shared.Models;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ManagerId { get; set; }
    public double Score { get; set; }
    public string Status { get; set; } = string.Empty;
    public Employee Manager { get; set; } = null!;
}