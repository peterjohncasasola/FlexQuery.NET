namespace FlexQuery.NET.Parsers.Jql;

internal sealed class JqlCollectionNode : JqlAstNode
{
    public JqlCollectionNode(string collectionPath, string quantifier, JqlAstNode filter)
    {
        CollectionPath = collectionPath;
        Quantifier = quantifier;
        Filter = filter;
    }

    public string CollectionPath { get; }
    public string Quantifier { get; }
    public JqlAstNode Filter { get; }

    public override string ToString() => $"{CollectionPath}.{Quantifier}({Filter})";
}