# Changelog

All notable changes to this project will be documented here.

---

## [1.1.0] - 2026-05-02
### Added
- **Query Debug Mode**: New `ToFlexQueryDebug()` extension method to inspect parsed AST and generated Expression Trees.
- **Expression Printer**: Custom visitor to translate internal Expression Trees into readable C#-like syntax.
- **AST Preservation**: `QueryOptions` now stores the parsed AST from JQL/DSL for debugging.
- `ToString()` overrides for JQL AST nodes for better visibility in debug logs.

## [1.0.0] - 2026-05-02
### Added
- Initial release of FlexQuery.NET
- Dynamic filtering, sorting, projection
- EF Core integration support

### Notes
- This is a rebranded and improved version of DynamicQueryable.Extensions