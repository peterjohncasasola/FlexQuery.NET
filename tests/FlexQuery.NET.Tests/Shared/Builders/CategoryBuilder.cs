namespace FlexQuery.NET.Tests.Shared.Builders;

public class CategoryBuilder
{
    private static int _nextId = 1;
    private int _id;
    private string _name = "Test Category";

    public CategoryBuilder WithId(int id) { _id = id; return this; }
    public CategoryBuilder WithName(string name) { _name = name; return this; }

    public Category Build()
    {
        return new Category
        {
            Id = _id == 0 ? _nextId++ : _id,
            Name = _name
        };
    }
}
