namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>Base type for DSL filter AST nodes.</summary>
/// <remarks>
/// Every AST node produced by <see cref="DslAstParser"/> is guaranteed to conform
/// to the DSL grammar. Invalid language constructs are rejected before AST construction.
/// Downstream components (converters, validators, expression builders) may rely on this
/// guarantee without re-validating grammar-level properties.
/// </remarks>
internal abstract class DslAstNode
{
}
