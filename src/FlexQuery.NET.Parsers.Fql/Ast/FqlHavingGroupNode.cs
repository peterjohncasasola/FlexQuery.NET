namespace FlexQuery.NET.Parsers.Fql;

internal sealed class FqlHavingGroupNode(FqlAstNode inner) : FqlAstNode
{
    public FqlAstNode Inner { get; } = inner;

    public override string ToString() => $"({Inner})";
}
