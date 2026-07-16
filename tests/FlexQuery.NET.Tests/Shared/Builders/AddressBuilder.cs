namespace FlexQuery.NET.Tests.Shared.Builders;

public class AddressBuilder
{
    private static int _nextId = 1;
    private int _id;
    private string? _street;
    private string? _city;
    private string? _province;
    private string? _country;
    private string? _postalCode;
    private Customer? _customer;

    public AddressBuilder WithId(int id) { _id = id; return this; }
    public AddressBuilder WithStreet(string? street) { _street = street; return this; }
    public AddressBuilder WithCity(string? city) { _city = city; return this; }
    public AddressBuilder WithProvince(string? province) { _province = province; return this; }
    public AddressBuilder WithCountry(string? country) { _country = country; return this; }
    public AddressBuilder WithPostalCode(string? postalCode) { _postalCode = postalCode; return this; }
    public AddressBuilder WithCustomer(Customer? customer) { _customer = customer; return this; }

    public Address Build()
    {
        return new Address
        {
            Id = _id == 0 ? _nextId++ : _id,
            Street = _street,
            City = _city,
            Province = _province,
            Country = _country,
            PostalCode = _postalCode,
            Customer = _customer,
            CustomerId = _customer?.Id ?? 0
        };
    }
}
