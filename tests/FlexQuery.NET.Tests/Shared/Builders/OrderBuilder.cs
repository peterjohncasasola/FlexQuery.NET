namespace FlexQuery.NET.Tests.Shared.Builders;

public class OrderBuilder
{
    private static int _nextId = 1;
    private int _id;
    private int _customerId;
    private Customer? _customer;
    private DateTime _orderDate = new DateTime(2023, 1, 1);
    private string _status = "Pending";
    private decimal _total;
    private decimal _price;
    private string _category = string.Empty;
    private string _number = string.Empty;
    private readonly List<OrderItem> _items = [];

    public OrderBuilder WithId(int id) { _id = id; return this; }
    public OrderBuilder WithCustomerId(int customerId) { _customerId = customerId; return this; }
    public OrderBuilder WithCustomer(Customer customer) { _customer = customer; return this; }
    public OrderBuilder WithOrderDate(DateTime orderDate) { _orderDate = orderDate; return this; }
    public OrderBuilder WithStatus(string status) { _status = status; return this; }
    public OrderBuilder WithTotal(decimal total) { _total = total; return this; }
    public OrderBuilder WithPrice(decimal price) { _price = price; return this; }
    public OrderBuilder WithCategory(string category) { _category = category; return this; }
    public OrderBuilder WithNumber(string number) { _number = number; return this; }
    public OrderBuilder AddItem(OrderItem item) { _items.Add(item); return this; }

    public Order Build()
    {
        return new Order
        {
            Id = _id == 0 ? _nextId++ : _id,
            CustomerId = _customerId == 0 && _customer != null ? _customer.Id : _customerId,
            Customer = _customer,
            OrderDate = _orderDate,
            Status = _status,
            Total = _total,
            Price = _price,
            Category = _category,
            Number = _number,
            OrderItems = _items
        };
    }
}
