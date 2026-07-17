namespace FlexQuery.NET.Parsers.Fql;

/// <summary>Base type for FQL AST nodes.</summary>
/// <remarks>
/// Every AST node produced by <see cref="FqlAstParser"/> is guaranteed to conform
/// to the FQL grammar. Invalid language constructs are rejected before AST construction.
/// Downstream components (converters, validators, expression builders) may rely on this
/// guarantee without re-validating grammar-level properties.
/// </remarks>
internal abstract class FqlAstNode
{
}