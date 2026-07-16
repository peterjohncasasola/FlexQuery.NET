namespace FlexQuery.NET.Tests.Shared.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<User> Users { get; set; } = [];
    public List<Permission> Permissions { get; set; } = [];
    public bool? IsActive { get; set; } = true;
}
