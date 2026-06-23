# Changelog

All notable changes to this project will be documented in this file.

---

## [3.0.4] - 2026-06-23

### Added

- **DefaultProjectionRule:** New validation rule (runs first in the pipeline) that auto-injects a governed `Select` when no explicit projection is specified. Priority: `SelectableFields` > `RoleAllowedFields` > `AllowedFields` > entity metadata minus `BlockedFields`.
- **Wildcard pattern expansion:** `DefaultProjectionHelper.ExpandWildcardFields` expands patterns like `Orders.*` by recursively walking navigation properties via reflection at injection time.
- **Non-strict re-apply:** When `Select` is emptied by NonStrict field removal, the default projection is automatically re-injected.
- **RoleAllowedFields auto-projection:** `RoleAllowedFields` now serves as a valid source for default projection injection.
- **GovernanceValidator:** New `GovernanceValidator.ValidateConfiguration()` for startup-time validation of governance config consistency — detects `BlockedFields` ∩ `AllowedFields` overlap and subset violations for `SelectableFields`/`FilterableFields`/`SortableFields`/`GroupableFields`/`AggregatableFields` vs `AllowedFields`.
- **GroupedSortValidator:** New `GroupedSortValidator.Validate()` in Core that centralizes grouped sort validation. Removes invalid sort fields (non-group-key, non-aggregate), resolves aggregate field names to aliases, and injects a group-key fallback when all sorts are invalid — ensuring deterministic paging for grouped queries.
- **New test coverage:** 24 FieldSecurityTests + 2 PagingTests covering default projection priority, wildcard expansion, non-strict re-apply, grouped query exclusion, no-governance fallthrough, config validation, and priority intersection. 6 new grouped query behavior tests across EF Core and Dapper providers (28 total grouped query tests).

### Changed

- **Paging fallback sort respects governance:** `QueryBuilder.ApplyPaging` now uses `options.Select?.FirstOrDefault()` before falling back to `Id`/`Key`/first property, ensuring deterministic pagination uses a governed field.
- **EF Core grouped sort resolution:** Replaced inline `BuildGroupedSorts<TShape>`/`ResolveGroupedSortField` with a shared call to `GroupedSortValidator.Validate`, removing the `TShape` generic parameter dependency and ensuring consistent sort validation.
- **Dapper grouped sort resolution:** `SqlTranslator.Translate` now validates sorts through `GroupedSortValidator.Validate` when the query has `GroupBy`, removing invalid ORDER BY columns that would cause SQL errors and injecting fallback sorts for deterministic paging.
- **Documentation:** Security & Governance guide updated with default projection, wildcard expansion, and config validation sections. New `docs/architecture/grouped-query-contract.md` documenting grouped query behavior across providers.

### Fixed

- **Default response field leak:** When `QueryOptions.Select` was null, the projection builder returned all entity fields regardless of governance configuration. Now governed defaults are injected before validation, ensuring `AllowedFields`, `BlockedFields`, and `RoleAllowedFields` are respected even for unprojected queries.
- **Dapper invalid SQL for grouped sorts:** Sort fields referencing entity columns not in GROUP BY (e.g., `ORDER BY [Id]` when grouping by `Category`) are now removed before SQL generation instead of producing invalid SQL.
- **Dapper nondeterministic grouped paging:** Grouped queries with paging but no valid ORDER BY now receive a deterministic fallback sort by the first group key ascending.

## [3.0.3] - 2026-06-23

### Fixed

- **AG Grid SSRM grouped sort validation:** `sortModel` entries at grouped levels are now validated against the current grouped projection. Aggregate sorts (e.g., `colId: "price"` with `aggFunc: "AVG"`) resolve to their SQL alias (`priceAvg`) instead of the raw column name, preventing invalid `ORDER BY Price` in Dapper grouped queries. Detail-column sorts (`colId: "id"`, `createdOn`) are discarded at grouped levels. Empty or fully-invalid sort models automatically inject the current group key ascending for deterministic pagination. `colId != field` mappings are resolved correctly. `"average"` is normalized to `"avg"` consistently with the aggregate builder. All existing behavior preserved at ungrouped and leaf levels.

### Behavioral Change

- **Aggregate alias naming convention redesigned:**
  Aggregate aliases now follow a field-first, camelCase format (`totalSum`, `idCount`, `priceAvg`) instead of the previous `FUNCTION_Field` convention (`SUM_Total`, `COUNT_Id`, `AVG_Price`). Full test suite: **669 tests passed**.

### Added

- **AgGrid SSRM response support:** `ToAgGridServerSideResponse()` extension method, `AgGridResponseConverter`, and new models (`AgGridGroupRow`, `AgGridLeafRow`, `AgGridResponseFieldOptions`, `AgGridServerSideResponse`). `AgGridRequest` gains `GroupKeys` for SSRM grouping levels.
- **QueryResult.ResultCount property:** Separate count for grouped/distinct queries vs `TotalCount`.
- **Dapper grouping & distinct support:** Full GROUP BY and DISTINCT support, `TranslateSourceCount()` on `ISqlTranslator`, `ExtractCountSql()` helper, and enhanced `ExecuteQueryAsync()` for dual counting.
- **New test coverage:** `AgGridResponseConverterTests`, `ResultCountTests`, `SqlTranslatorGroupedTests`, `GroupedQueryExecutionTests`.

### Changed

- **AgGrid column models:** Added `Id` property to `AgGridGroupColumn` and `AgGridValueColumn` to capture the column identifier from AG Grid payloads.
- **AgGrid request deserialization:** `DeserializeRequest` now captures the `id` field from `rowGroupCols` and `valueCols` JSON objects, enabling `colId != field` sort resolution.
- **Documentation:** AgGrid adapter, migration guide, paging guide, grouping/projection/examples docs, and Dapper provider docs updated.

## [3.0.2] - 2026-06-22

### Added

- **Simplified AG Grid API:** New extension methods — `AgGridRequest.ToQueryOptions()`, `QueryOptions.ApplyAgGridRequest(AgGridRequest)`, `string.FromAgGridJson()` — removing the need to reference `AgGridQueryOptionsParser` directly.

### Changed

- **Documentation:** README and AG Grid adapter guide updated for the simplified API.

## [3.0.1] - 2026-06-22

### Breaking Changes

- **JQL parser removed from FlexQuery.NET Core:** JQL support has been extracted into the dedicated `FlexQuery.NET.Parsers.Jql` package.
  - Install `FlexQuery.NET.Parsers.Jql` to continue using JQL filter expressions.
  - `JqlQueryParser` is no longer available from the Core package.
  - `QuerySyntax.Jql` has been removed.

- **`FlexQuery.NET.AgGrid` → `FlexQuery.NET.Adapters.AgGrid`:**
  - Package renamed for consistent adapter naming.
  - Update NuGet package references, namespaces, and `using` directives.

- **`FlexQuery.NET.Kendo` → `FlexQuery.NET.Adapters.Kendo`:**
  - Package renamed for consistent adapter naming.
  - Update NuGet package references, namespaces, and `using` directives.

- **`FlexQuery.NET.EFCore` → `FlexQuery.NET.EntityFrameworkCore`:**
  - Package and namespace renamed for clarity.
  - Update NuGet package references and `using` directives.

- **JQL fallback removed from `FilteredIncludeParser`:**
  - Inline include filters no longer support JQL-style expressions.
  - The following syntax is no longer supported:

    ```csharp
    orders(Status = 'Cancelled')
    ```

  - Use FlexQuery DSL syntax instead:

    ```csharp
    orders(Status:eq:Cancelled)
    ```

  - Unsupported JQL syntax now throws `InvalidOperationException`.

- **Removed deprecated APIs:**
  - `QueryRequest`
  - `FlexQueryRequest`
  - `ApplyValidatedQueryOptions`
  - `ToQueryResultAsync`
  - `ToProjectedQueryResultAsync`
  - Other APIs previously marked `[Obsolete]`

- **`JqlParser.Parse(string query)` → `JqlParser.Parse(string filter)`:**
  - Parameter renamed to align with DSL and MiniOData terminology.
  - Callers using named arguments must update their code.

> **Note:** Although this release contains several breaking changes, it is being released as **3.0.1** because the initial **3.0.0** release had near-zero adoption and no published migration guides.

### Added

- **FlexQuery.NET.Parsers.Jql package:** JQL filter parser extracted from FlexQuery.NET Core into a dedicated install-on-demand package.

### Changed

- **`FilteredIncludeParser` behavior:** Deprecated JQL-style include filters now throw `InvalidOperationException` with a descriptive migration message instead of silently returning `null`.

### Fixed

- No functional fixes in this release.

---

## [3.0.0] - 2026-06-20

### Added

- **FlexQuery.NET.Dapper package:** Dapper and raw SQL provider with dialect support for SQL Server, SQLite, MySQL, and PostgreSQL.
- **FlexQuery.NET.AgGrid package:** AG Grid Enterprise Server-Side Row Model request adapter.
- **FlexQuery.NET.Kendo package:** Kendo UI DataSource request adapter.
- **FlexQuery.NET.Parsers.MiniOData package:** OData-style query parameter parser (`$filter`, `$orderby`, `$select`, `$top`, `$skip`, `$expand`).
- **Grand Total Aggregations and Having support**
- **Non-strict validation mode** (`StrictFieldValidation`): Silently removes unauthorized fields instead of throwing.
- **Flat projection support** (`mode=flat`, `mode=flat-mixed`)
- **DTO field mapping** via `MapField()`
- **.NET 10 target framework support**

### Changed

- **Provider-agnostic architecture:** Query engine decoupled from Entity Framework to support multiple backends.
- **Parser pipeline refactoring:** Monolithic parser decomposed into focused parser components.
- **Query caching and expression generation optimizations**

### Fixed

- **EF Core collection translation issues**
- **SQLite translation issues for collection navigation filters**

### Removed

- **.NET 7 support**
- **Deprecated `QueryRequest`**
- **Deprecated `FlexQueryRequest`**
- **Legacy APIs previously marked obsolete**

---
## [2.5.0] - 2026-05-10

### Added
- **AllowedIncludes Governance**: Introduced `AllowedIncludes` to `QueryExecutionOptions` to govern which navigation properties/relationships can be expanded or included.
- **Separate Include Validation**: Implemented `IncludeAccessValidator` that validates both flat and filtered includes against the `AllowedIncludes` whitelist.
- **Strict Matching**: `AllowedIncludes` requires explicit path definition (e.g., `"Orders.Items"`) and does not automatically expand wildcard nested fields, keeping include security separate from field projection logic.

## [2.4.0] - 2026-05-10

### Added
- **Two-Tier Configuration Architecture**: Introduced `FlexQueryOptions` for configuring global application-wide defaults via DI (`builder.Services.AddFlexQuery(options => ...)`).
- **Per-Request Overrides**: `QueryExecutionOptions` now uses nullable properties to allow per-request overrides that fall back to the global `FlexQueryOptions` defaults.
- **ASP.NET Core DI Extensions**: Added `AddFlexQuery` extension method to `IServiceCollection` in `FlexQuery.NET.AspNetCore` for easy global configuration registration.

### Changed
- **Separated Security from Execution Defaults**: Infrastructure default properties (`MaxPageSize`, `DefaultPageSize`, `CaseInsensitive`, `IncludeTotalCount`, `StrictFieldValidation`, `MaxFieldDepth`, `UseNoTracking`) were migrated from `QueryExecutionOptions` to the global `FlexQueryOptions`.
- **Internal Execution Options**: `EffectiveQueryOptions` has been marked as `internal` and is no longer part of the public API.
- **Documentation Overhaul**: Restructured performance documentation into a dedicated `docs/guide/performance` directory and comprehensively updated getting-started, security, validation, and migration guides to reflect the new configuration architecture.

## [2.1.0] - 2026-05-07

### Added
- **EF Core Split Query Support**: Added `UseSplitQuery` to `QueryExecutionOptions`, allowing servers to opt into `.AsSplitQuery()` for complex include trees to prevent cartesian explosion.
- **No-Tracking Execution**: Added `UseNoTracking` to `QueryExecutionOptions` (defaulting to `true`) to automatically apply `.AsNoTracking()` in the `FlexQueryAsync` pipeline.
- **Execution Strategy Control**: Servers now have granular control over *how* queries are executed (Split Query, Tracking), while clients remain focused on *what* data to retrieve.
- **Per-Field Operator Governance**: Implemented strict server-side validation for operators. Developers can now restrict which operators are permissible per field using `QueryExecutionOptions.AllowOperators()` using canonical `FilterOperators` constants.

### Documentation
- **Performance Optimization**: Added a dedicated section on "Split Query Optimization" to the Performance and Include Filtering guides.
- **Architecture Notes**: Clarified the responsibility of the server to define execution strategies, keeping query parameters strictly focused on data requirements.

## [2.0.0] - 2026-05-06

### Breaking Changes
- **Dual-Model Architecture**: Successfully decoupled query input (`QueryOptions`) from server-side execution policy (`QueryExecutionOptions`).
- **Security Logic Migration**: Migrated all security configuration properties (`AllowedFields`, `BlockedFields`, `FilterableFields`, `SortableFields`, `SelectableFields`, `MaxFieldDepth`) out of `QueryOptions` and into the dedicated `QueryExecutionOptions` class.
- **API Signatures**: Updated `ApplyFlexQuery` and `ApplyFlexQueryAsync` to require or optionally accept `QueryExecutionOptions`. The redundant `ApplyValidatedQueryOptions` has been removed.

#### Added
- **High-Level `FlexQueryAsync` API**: A unified entry point that orchestrates parsing, validation, and execution (Filtering, Sorting, Paging, Includes, and Projection) in a single secure pass.
- **Dual-Pipeline Architecture**: Successfully decoupled root entity filtering (WHERE) from related data shaping (Filtered Includes), solving the "over-filtering" regression in nested collections.
- **Unified Projection Engine**: `ProjectionBuilder` now automatically merges `FilteredIncludes` and dynamic `Select` paths into a single optimized `Select()` statement.
- **Canonical Query Normalization**: Added a filter AST normalizer that deterministically orders conditions for efficient expression caching.
- **FlexQueryParameters DTO**: Introduced an OpenAPI-friendly DTO with full XML documentation for better Swagger UI integration.

### Changed
- **Security Logic Decoupling**: Migrated all security policies (`AllowedFields`, `MaxFieldDepth`, etc.) out of `QueryOptions` and into the trusted `QueryExecutionOptions` model.
- **Standardized Parameter Mapping**: `QueryOptionsParser` now uses `include` and `group` as canonical keys, ensuring consistency across JQL, DSL, and JSON formats.
- **Public Filter Model**: `QueryOptions.Filter` now uses the public `FilterGroup` model; implicit conversions to internal `FilterGroupNode` preserve `IsNegated` and `ScopedFilter` semantics.
- **Deprecated Legacy APIs**: `ToQueryResultAsync` and `ToProjectedQueryResultAsync` are now deprecated in favor of the unified `FlexQuery` pipeline.

### Bug Fixes
- **Filtered Include Regression**: Fixed an issue where filters on nested navigation properties were incorrectly applied or ignored during projection.
- **Parameter Key Mismatch**: Resolved a bug where `Includes` and `GroupBy` properties were ignored by the parser due to case-sensitivity and key naming mismatches.
- **Filter Negation**: Fixed `FilterGroup` conversion logic to correctly propagate the `IsNegated` flag to the expression builder.
- **Field Access Validation**: Hardened the `FieldAccessValidator` to correctly validate nested property paths against depth limits and whitelists.
(resolving ambiguous `Normalize()` method references).
- Fixed `QueryOptions` cache key string formatting errors (updated `Take` to `Top` and `OrderBy` to a properly mapped `Sort` value).

### Documentation
- **Comprehensive v2 Refactor**: Reorganized all documentation into a versioned structure (`docs/v1` and `docs/guide` for v2).
- **Unified Migration Guide**: Consolidated all v1 → v2 migration steps into a single, clean `/migration` path.
- **Fluent API Documentation**: Updated all guides to prioritize fluent `IQueryable` extension methods over legacy static builders.
- **Execution & Security**: Added dedicated sections for the unified execution pipeline (`FlexQueryAsync`) and trusted server-side rules.
- **Aggregate Syntax**: Documented the new LINQ-style aggregate syntax (e.g., `status.count()`) in the Projection guide.


## [1.7.0] - 2026-05-03

### Added
- **Expression Caching Engine**: Implemented a thread-safe caching system for LINQ Expression trees.
- **`FlexQueryCacheSettings`**: Global configuration for cache size, enabling/disabling, and compiled delegate caching.
- **Normalized Cache Keys**: Stable key generation using `FilterAnalyzer.CacheKey`, ensuring consistent hits across identical query structures regardless of property order.
- **`QueryOptions.EnableCache`**: Per-query cache control to override global settings.

### Performance
- **90%+ Reduction in Query Preparation**: Caching the expression tree building phase significantly reduces CPU overhead and heap allocations for repetitive query patterns.
- **Thread-Safe Caching**: Leverages `ConcurrentDictionary` for high-concurrency scenarios with built-in size limits to prevent memory leaks.

## [1.6.0] - 2026-05-03

### Performance
- **Eliminated `LOWER()` from SQL string comparisons** — `SafeConditionBuilder` no longer wraps member expressions with `.ToLower()` before applying `Contains`, `StartsWith`, `EndsWith`, or `=`. This means EF Core now generates clean `LIKE` and `=` predicates without function calls, allowing SQL Server to use column indexes on string fields.

### Added
- **`QueryOptions.CaseInsensitive`** (`bool`, default `true`) — New configuration property. When `true` (the default), case-insensitive matching is delegated to the **database collation** (SQL Server default `SQL_Latin1_General_CP1_CI_AS` is case-insensitive). When set to `false`, string comparisons behave as strict ordinal matches.
- **`QueryOptions.CloneWithFilter(FilterGroup?)`** — Internal helper that creates a shallow clone of `QueryOptions` with a different `Filter`. Used by the nested projection and include pipelines to propagate query configuration without mutation.
- **Backward-compatible `BuildPredicate` overloads** — `ExpressionBuilder.BuildPredicate<T>(FilterGroup)` and `BuildPredicate(Type, FilterGroup)` are retained as convenience wrappers (default `CaseInsensitive = true`).

### Changed
- `ExpressionBuilder.BuildPredicate<T>` primary overload now accepts `QueryOptions` to carry the `CaseInsensitive` flag through the full expression tree recursion (including `Any`, `All`, `Count`, and scoped collection expressions).
- `ProjectionBuilder.Build<T>` now accepts `QueryOptions` (previously accepted only `FilterGroup`). The cache key includes the `CaseInsensitive` flag.
- `ProjectionEnhancer.ApplyCollectionWhereIfNeeded` now accepts `QueryOptions` and propagates it to nested predicate builders.
- `IncludeBuilder.Apply` now accepts `QueryOptions` (previously accepted `IEnumerable<IncludeNode>`). Options are threaded through the full include tree recursion.
- `HavingExpressionBuilder.Build` now accepts a `caseInsensitive` parameter.

### SQL Output — Before vs After

**Before:**
```sql
WHERE LOWER([c].[Name]) LIKE '%john%'
  AND LOWER([o].[Status]) = 'cancelled'
```

**After:**
```sql
WHERE [c].[Name] LIKE '%John%'
  AND [o].[Status] = 'Cancelled'
```

### Notes
- **SQL Server**: The default collation (`CI_AS`) is case-insensitive, so removing `LOWER()` preserves the existing case-insensitive behavior while enabling index seeks.
- **PostgreSQL**: Default collation is case-sensitive. If you target PostgreSQL and require case-insensitive matching, either configure a `citext` column type, a `ICU` collation, or use `EF.Functions.ILike`.
- **In-memory / SQLite providers**: String comparison is case-sensitive by nature. Tests against in-memory databases must use values matching the exact stored casing.

---

## [1.5.0] - 2026-05-02
### Added
- **Secure Input Binding**: Introduced `QueryRequest` DTO to decouple HTTP input from internal execution logic, preventing malicious clients from overriding server-side security settings (e.g., `AllowedFields`).
- **Parser Overload**: Added `QueryOptionsParser.Parse(QueryRequest)` to securely map primitive DTO properties to the internal execution model while retaining 100% backward compatibility for existing parsing logic.

### Changed
- **Documentation Refactor**: Completely updated `README.md` to introduce the new `QueryRequest` usage pattern, detailed real-world query examples, and comprehensive field-level security documentation.

## [1.4.0] - 2026-05-02

### Added
- **Field-Level Security**: Added comprehensive security properties to `QueryOptions` (whitelisting, blacklisting, roles).
- **Field Processing Pipeline**: Implemented an 8-step pipeline with wildcard/regex support for granular field access control.
- **ASP.NET Core Integration**: Added `[FieldAccess]` attribute and action filter for declarative API security.


### Changed
- **Pipeline Security**: Moved security validation to the front of the pipeline for fail-fast safety.
- **Core Decoupling**: Decoupled core security logic from EF Core and optimized for .NET 6-8.

- **Test Coverage**: Verified library stability with 204 unit tests (100% pass rate).

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