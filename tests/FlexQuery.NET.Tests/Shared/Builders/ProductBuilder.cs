namespace FlexQuery.NET.Tests.Shared.Builders;

public class ProductBuilder
{
    private static int _nextId = 1;
    private int _id;
    private string _sku = "SKU-001";
    private string _name = "Test Product";
    private string? _description;
    private decimal _price = 10m;
    private bool _isActive = true;
    private int _categoryId;
    private Category? _category;

    public ProductBuilder WithId(int id) { _id = id; return this; }
    public ProductBuilder WithSku(string sku) { _sku = sku; return this; }
    public ProductBuilder WithName(string name) { _name = name; return this; }
    public ProductBuilder WithDescription(string? description) { _description = description; return this; }
    public ProductBuilder WithPrice(decimal price) { _price = price; return this; }
    public ProductBuilder WithIsActive(bool isActive) { _isActive = isActive; return this; }
    public ProductBuilder WithCategoryId(int categoryId) { _categoryId = categoryId; return this; }
    public ProductBuilder WithCategory(Category? category) { _category = category; return this; }

    public Product Build()
    {
        return new Product
        {
            Id = _id == 0 ? _nextId++ : _id,
            SKU = _sku,
            Name = _name,
            Description = _description,
            Price = _price,
            IsActive = _isActive,
            CategoryId = _categoryId == 0 && _category != null ? _category.Id : _categoryId,
            Category = _category
        };
    }
}
