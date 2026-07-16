namespace FlexQuery.NET.Tests.Shared.Builders;

public class UserBuilder
{
    private static int _nextId = 1;
    private int _id;
    private string _userName = "testuser";
    private string? _email;
    private bool _isActive = true;
    private readonly List<Role> _roles = new();

    public UserBuilder WithId(int id) { _id = id; return this; }
    public UserBuilder WithUserName(string userName) { _userName = userName; return this; }
    public UserBuilder WithEmail(string? email) { _email = email; return this; }
    public UserBuilder WithIsActive(bool isActive) { _isActive = isActive; return this; }
    public UserBuilder AddRole(Role role) { _roles.Add(role); return this; }

    public User Build()
    {
        return new User
        {
            Id = _id == 0 ? _nextId++ : _id,
            UserName = _userName,
            Email = _email,
            IsActive = _isActive,
            Roles = _roles
        };
    }
}
