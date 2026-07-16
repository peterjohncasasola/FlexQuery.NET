namespace FlexQuery.NET.Tests.Shared.Builders;

public class RoleBuilder
{
    private static int _nextId = 1;
    private int _id;
    private string _name = "TestRole";
    private string? _description;
    private readonly List<User> _users = new();
    private readonly List<Permission> _permissions = new();

    public RoleBuilder WithId(int id) { _id = id; return this; }
    public RoleBuilder WithName(string name) { _name = name; return this; }
    public RoleBuilder WithDescription(string? description) { _description = description; return this; }
    public RoleBuilder AddUser(User user) { _users.Add(user); return this; }
    public RoleBuilder AddPermission(Permission permission) { _permissions.Add(permission); return this; }

    public Role Build()
    {
        return new Role
        {
            Id = _id == 0 ? _nextId++ : _id,
            Name = _name,
            Description = _description,
            Users = _users,
            Permissions = _permissions
        };
    }
}
