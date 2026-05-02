# Changelog

All notable changes to this project will be documented in this file.

---

## [1.2.0] - 2026-05-02

### Added
- **Query Validation Engine**: Pipeline-based validation for `QueryOptions`
- **Field-Level Security**:
  - `AllowedFields` (whitelisting)
  - `BlockedFields` (blacklisting)
  - Supports nested property paths
- **Fail-Fast Safety**:
  - Introduced `ApplyValidatedQueryOptions` for early validation in the query pipeline
- **Validation Rules**:
  - Field existence validation
  - Operator validity validation
  - Type compatibility validation

### Changed
- Standardized all namespaces to `FlexQuery.NET`
- Refactored validation rules into modular components for better maintainability

---

## [1.1.1] - 2026-05-02

### Changed
- Improved CI/CD pipeline to support formatted release notes generation
- Enhanced consistency between GitHub Releases and NuGet metadata

### Maintenance
- Refined build and publish workflow
- Minor pipeline reliability improvements

---

## [1.1.0] - 2026-05-02

### Added
- **Query Debug Mode**:
  - `ToFlexQueryDebug()` extension method for inspecting parsed AST and Expression Trees
- **Expression Printer**:
  - Converts internal Expression Trees into readable C#-like syntax
- **AST Preservation**:
  - `QueryOptions` now stores parsed AST from JQL/DSL
  - Improved `ToString()` for AST nodes

- **Query Validation Engine (Initial Version)**:
  - Pre-expression validation pipeline
  - Field existence validation
  - Operator validation (`=`, `!=`, `>`, `<`, `contains`, etc.)
  - Type compatibility validation
  - Nested navigation validation
  - Collection validation (`any`, `all`)
  - Introduced `ValidationResult`:
    - `IsValid`
    - `Errors` (detailed messages)
    - `Error Codes` (API-friendly)

- **Extensible Architecture**:
  - `IQueryValidator` interface
  - Pluggable validators (`FieldValidator`, `OperatorValidator`, `TypeValidator`, etc.)
  - Pipeline-based validation flow
  - Support for custom validators (plugin-style)

### Changed
- Validation now executes **before expression building**
- Improved CI/CD pipeline and release notes formatting
- Enhanced consistency between GitHub Releases and NuGet metadata

### Maintenance
- Refined build and publish workflow
- Minor reliability improvements

---

## [1.0.0] - 2026-05-02

### Added
- Initial release of **FlexQuery.NET**
- Dynamic filtering, sorting, projection, and pagination
- EF Core integration support

### Notes
- Rebranded and redesigned from `DynamicQueryable.Extensions`
- Version history from the previous project is not carried over