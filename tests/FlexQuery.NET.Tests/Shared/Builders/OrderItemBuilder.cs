namespace FlexQuery.NET.Tests.Shared.Builders;

public class OrderItemBuilder
{
    private static int _nextId = 1;
    private int _id;
    private int _orderId;
    private Order? _order;
    private int? _productId;
    private Product? _product;
    private int _quantity = 1;
    private decimal _unitPrice = 10m;
    private decimal _price;

    public OrderItemBuilder WithId(int id) { _id = id; return this; }
    public OrderItemBuilder WithOrderId(int orderId) { _orderId = orderId; return this; }
    public OrderItemBuilder WithOrder(Order order) { _order = order; return this; }
    public OrderItemBuilder WithProductId(int? productId) { _productId = productId; return this; }
    public OrderItemBuilder WithProduct(Product? product) { _product = product; return this; }
    public OrderItemBuilder WithQuantity(int quantity) { _quantity = quantity; return this; }
    public OrderItemBuilder WithUnitPrice(decimal unitPrice) { _unitPrice = unitPrice; return this; }
    public OrderItemBuilder WithPrice(decimal price) { _price = price; return this; }

    public OrderItem Build()
    {
        return new OrderItem
        {
            Id = _id == 0 ? _nextId++ : _id,
            OrderId = _orderId == 0 && _order != null ? _order.Id : _orderId,
            Order = _order,
            ProductId = _productId,
            Product = _product,
            Quantity = _quantity,
            UnitPrice = _unitPrice,
            Price = _price
        };
    }
}
