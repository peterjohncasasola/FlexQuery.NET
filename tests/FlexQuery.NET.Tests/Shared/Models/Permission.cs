namespace FlexQuery.NET.Tests.Shared.Models;

public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Role> Roles { get; set; } = [];
}
