namespace FlexQuery.NET.Tests.Shared.Builders;

public class CustomerBuilder
{
    private static int _nextId = 1;
    private int _id;
    private string _name = "Test Customer";
    private string? _email;
    private string? _phone;
    private int _age = 30;
    private string? _city;
    private string _status = "Active";
    private bool _isActive = true;
    private DateTime _createdAt = new DateTime(2023, 1, 1);
    private string? _ssn;
    private decimal _salary = 50000m;
    private string _category = "Standard";
    private string _secretField = string.Empty;
    private Address? _primaryAddress;
    private readonly List<Address> _addresses = new();
    private Profile? _profile;
    private readonly List<Order> _orders = new();

    public CustomerBuilder WithId(int id) { _id = id; return this; }
    public CustomerBuilder WithName(string name) { _name = name; return this; }
    public CustomerBuilder WithEmail(string? email) { _email = email; return this; }
    public CustomerBuilder WithPhone(string? phone) { _phone = phone; return this; }
    public CustomerBuilder WithAge(int age) { _age = age; return this; }
    public CustomerBuilder WithCity(string? city) { _city = city; return this; }
    public CustomerBuilder WithStatus(string status) { _status = status; return this; }
    public CustomerBuilder WithIsActive(bool isActive) { _isActive = isActive; return this; }
    public CustomerBuilder WithCreatedAt(DateTime createdAt) { _createdAt = createdAt; return this; }
    public CustomerBuilder WithSSN(string? ssn) { _ssn = ssn; return this; }
    public CustomerBuilder WithSalary(decimal salary) { _salary = salary; return this; }
    public CustomerBuilder WithCategory(string category) { _category = category; return this; }
    public CustomerBuilder WithSecretField(string secretField) { _secretField = secretField; return this; }
    public CustomerBuilder WithPrimaryAddress(Address? address) { _primaryAddress = address; return this; }
    public CustomerBuilder WithProfile(Profile? profile) { _profile = profile; return this; }
    public CustomerBuilder AddAddress(Address address) { _addresses.Add(address); return this; }
    public CustomerBuilder AddOrder(Order order) { _orders.Add(order); return this; }

    public Customer Build()
    {
        return new Customer
        {
            Id = _id == 0 ? _nextId++ : _id,
            Name = _name,
            Email = _email,
            Phone = _phone,
            Age = _age,
            City = _city,
            Status = _status,
            IsActive = _isActive,
            CreatedAt = _createdAt,
            SSN = _ssn,
            Salary = _salary,
            Category = _category,
            SecretField = _secretField,
            Address = _primaryAddress,
            Addresses = _addresses,
            Profile = _profile,
            Orders = _orders
        };
    }
}
