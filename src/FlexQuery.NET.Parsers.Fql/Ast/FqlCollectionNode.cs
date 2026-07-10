namespace FlexQuery.NET.Parsers.Fql;

internal sealed class FqlCollectionNode : FqlAstNode
{
    public FqlCollectionNode(string collectionPath, string quantifier, FqlAstNode filter)
    {
        CollectionPath = collectionPath;
        Quantifier = quantifier;
        Filter = filter;
    }

    public string CollectionPath { get; }
    public string Quantifier { get; }
    public FqlAstNode Filter { get; }

    public override string ToString() => $"{CollectionPath}.{Quantifier}({Filter})";
}