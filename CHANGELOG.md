# Changelog

All notable changes to this project will be documented in this file.

---

## [1.3.1] - 2026-05-02

### Maintenance
- Synchronized package versions across the solution.
- Internal cleanup and project structure optimization.

---

## [1.3.0] - 2026-05-02

### Added
- **FlexQuery.NET.AspNetCore Package**:
  - New integration package for ASP.NET Core applications.
  - `[FieldAccess]` attribute for declarative controller and action-level security.
  - `FieldAccessFilter` for automatic discovery and application of security rules.
  - `AddFlexQuerySecurity()` extension method for easy global registration.
- **Advanced Security Features**:
  - **Regex Wildcard Support**: Broad permissions via `*` (e.g., `Orders.*`) using optimized regex matching.
  - **MaxFieldDepth**: New configuration to limit the complexity of nested property paths.
- **Enhanced Validation Engine**:
  - Introduced `QueryContext` for passing request-specific metadata through the validation pipeline.
  - Refactored `IQueryValidator` and `IValidationRule` to support context-aware validation.
  - `TypeCompatibilityRule` now performs deep value conversion attempts to ensure data integrity.

### Changed
- Standardized all namespaces to `FlexQuery.NET` for better consistency across packages.
- Improved error messaging for type mismatch and depth exceeded errors.

---

## [1.2.0] - 2026-05-02

### Added
- **Query Validation Engine**: Initial implementation of the pipeline-based validation for `QueryOptions`.
- **Field-Level Security**: Basic support for `AllowedFields` (whitelisting) and `BlockedFields` (blacklisting).
- **Fail-Fast Safety**: Introduced `ApplyValidatedQueryOptions` for early validation in the query pipeline.
- **Validation Rules**: Initial rules for field existence, operator validity, and type compatibility.

---

## [1.1.1] - 2026-05-02

### Changed
- Improved CI/CD pipeline to support formatted release notes generation.
- Enhanced consistency between GitHub Releases and NuGet metadata.

### Maintenance
- Refined build and publish workflow.
- Minor pipeline reliability improvements.

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
- Initial release of **FlexQuery.NET**.
- Dynamic filtering, sorting, projection, and pagination.
- EF Core integration support.

### Notes
- Rebranded and redesigned from `DynamicQueryable.Extensions`.