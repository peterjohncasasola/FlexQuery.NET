namespace FlexQuery.NET.Tests.Shared.Builders;

public class PermissionBuilder
{
    private static int _nextId = 1;
    private int _id;
    private string _code = "PERM_001";
    private string? _description;
    private readonly List<Role> _roles = new();

    public PermissionBuilder WithId(int id) { _id = id; return this; }
    public PermissionBuilder WithCode(string code) { _code = code; return this; }
    public PermissionBuilder WithDescription(string? description) { _description = description; return this; }
    public PermissionBuilder AddRole(Role role) { _roles.Add(role); return this; }

    public Permission Build()
    {
        return new Permission
        {
            Id = _id == 0 ? _nextId++ : _id,
            Code = _code,
            Description = _description,
            Roles = _roles
        };
    }
}
