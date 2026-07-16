namespace FlexQuery.NET.Tests.Shared.Models;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public List<Role> Roles { get; set; } = [];
}
