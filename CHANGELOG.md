# Changelog

All notable changes to this project will be documented in this file.

---

## [2.0.1] - 2026-06-30

### ✨ Enhancements
- Extended JQL parser with full SQL-like syntax support
- Improved tokenizer to seamlessly handle case-insensitivity and quoted values

### 🔍 New Features
- Added support for operators:
  - `IS NULL`
  - `IS NOT NULL`
  - `BETWEEN`
  - `LIKE`
  - `STARTSWITH`
  - `ENDSWITH`

- Added support for collection operators:
  - `ANY`
  - `ALL`
  - `COUNT`

- Added `ALL` operator to the core expression builder

---

## [2.0.0] - 2026-06-30

### 💥 Breaking Changes

- removed Spatie (Laravel-style) query format support
- removed Syncfusion query adapter and parsing
- removed Syncfusion-style sorting support
- removed format detection for legacy query formats

### ✨ Improvements

- standardized query formats:
  - DSL (primary)
  - JSON (advanced)
  - Indexed (compatibility)
- simplified format detection logic (indexed → JSON → DSL)
- removed adapter abstraction layer
- reduced parsing complexity and improved maintainability
- improved consistency across filtering, sorting, and selection

---

### 🏗 Internal

- removed SpatieAdapter and SyncfusionAdapter
- removed adapter factory and related interfaces (if unused)
- refactored query parsing pipeline
- cleaned up dead code and legacy mappings

---

### 🔒 Compatibility

- DSL queries continue to work without changes
- indexed format remains supported for ASP.NET model binding
- JSON format remains fully supported

---

## ⚠️ Migration Required

If you were using Spatie or Syncfusion formats, you must migrate to DSL or indexed format.

See Migration Guide below.

---

## [1.9.0]

### ✨ Features

- add JQL-style aggregation support (group by, aggregates, having)
- support `group` parameter for multi-field grouping
- extend `select` to support aggregate functions (`sum`, `count`, `avg`)
- implement `having` clause for post-group filtering

---

### 🏗 Internal

- introduce `AggregateModel`
- add `GroupByBuilder`
- extend `SelectBuilder` for aggregate handling
- add `HavingExpressionBuilder`
- integrate aggregation pipeline with existing `QueryOptions`

---

### 📚 Documentation

- update README with examples for grouping, aggregates, and having usage

---

### 🔒 Compatibility

- no breaking changes
- existing DSL filtering, sorting, and select remain unchanged

---

## [1.8.0]

### ✨ Features

- add advanced DSL filtering operators:
  - NOT operator (`!`, `not(...)`)
  - LIKE operator with pattern support
  - ANY operator for collection filtering
  - COUNT operator for collection size filtering

---

### 🧠 Improvements

- add aggregate-aware filtering for collections
- improve LIKE operator with fallback patterns (`contains`, `startsWith`, `endsWith`)
- introduce pluggable operator handler system for extensibility

---

### 🏗 Refactor

- remove direct EF Core dependency (EF-agnostic implementation)
- decouple operator logic from EF-specific functions

---

### 📚 Documentation

- update README with new operator usage and examples

---

### 🔒 Compatibility

- no breaking changes
- existing DSL queries continue to work

---

## [1.7.0]

### ✨ Features

- added aggregate support  
- added multi-field sort parser  
- applied dynamic sorting  
- integrated sorting with `QueryOptions`  
- added aggregate-based sorting for collection navigation  

---

### 🧪 Tests

- added sorting tests for nested and aggregate fields  

---

### 📚 Documentation

- updated sorting guide with aggregate function support  

---


## [1.6.4]

### ✨ Features

- add nested and multi-field sort parsing  

---

### 🐛 Fixes

- fix release pipeline issues  
- correct CI release workflow  

---

### ⚙️ CI / DevOps

- add automated release workflow  

---

### 📚 Documentation

- update README  

---


## [1.5.0]

### ✨ Features

- added JQL-style filtering support (Jira-like queries)  

---

### ⚡ Improvements

- refactored JQL parser to reuse operator registry  
- reused property resolver across DSL, JSON, and JQL  
- reduced duplicated parsing logic  

---

### 🧠 Internal

- unified parsing pipeline for all filter types  
- improved maintainability and extensibility  

---

### 🐛 Fixes

- fixed inconsistencies between DSL and JSON filters  

---




## [1.4.0]

### ✨ Features

- enhanced DSL value parsing to support complex values  
- supports unquoted values with spaces and special characters  
- supports escape sequences (backslash) in values  
- handles values containing colons (e.g. URLs, timestamps)  
- added support for filtered nested collection projection  
- allows applying filters on nested collections when included in `select`  
- enables more precise data shaping for related entities  

---

### ⚡ Improvements

- improved DSL tokenizer to correctly parse values after multiple delimiters  
- expanded safe character support for DSL filtering (e.g. emails, URLs)  
- optimized nested filtering using `EXISTS` to avoid unnecessary joins  
- prevented over-fetching of unrelated child collection data  

---

### 📚 Documentation

- updated DSL documentation to clarify handling of complex values  
- added guidance on nested filtering and projection behavior  

---

### 🧪 Tests

- added comprehensive test coverage for:
  - emails, URLs, and special characters in values  
  - values with spaces and colons  


## [1.3.0]

### ✨ Features

- introduced DSL (Domain-Specific Language) query parser for SQL-like filtering  
- supports expressions like: `name eq John and age gt 18 or status eq Active`  
- added logical operators: `and`, `or`, `not`  
- added comparison operators: `eq`, `ne`, `gt`, `lt`, `ge`, `le`  
- added string operators: `contains`, `startswith`, `endswith`, `in`  
- implemented AST-based parsing for structured and extensible query processing  
- enabled conversion of DSL queries into unified `FilterGroup` format  

---

### 🔐 Security

- introduced secure query validation layer to prevent unsafe expressions  
- added property access validation to restrict invalid or unsafe fields  
- implemented AST traversal validation to guard against injection-like patterns  

---

### ⚡ Improvements

- enhanced expression generation for DSL-based filters  
- improved extensibility via operator and field registries  
- ensured safe and predictable runtime query parsing  

---

### 📚 Documentation

- added DSL query format documentation with examples  
- updated README with usage guidelines  

---

### 🧪 Tests

- added comprehensive test coverage for DSL parsing and conversion  
- all tests passing (105 total)  




## [1.2.0]

### ✨ Features

- improved projection behavior when using `Include` without explicit `Select`  
- automatically includes root entity scalar fields alongside navigation properties  
- added `ToProjectedQueryResult` extension for shaping query results into dynamic projections  
- introduced support for Syncfusion and Laravel Spatie filter formats  

---

### ⚡ Improvements

- enhanced query shaping when combining `Include` and projection  
- improved compatibility by adding .NET Standard support  
- refactored Spatie filter parser into a dedicated module  
- enhanced Spatie filter parsing to support nested AND/OR groupings  

---

### 📚 Documentation

- added comprehensive examples for:
  - Syncfusion filtering (basic usage, logical conditions, nested properties)  
  - Laravel Spatie filtering (basic usage, implicit AND, nested properties)  
- clarified limitations of Spatie format (AND-only, no explicit OR support in standard usage)  
- documented nested property support via dot notation  
- improved overall README structure and readability  

---

### 🧪 Tests

- added integration tests for Syncfusion and Spatie filter formats  
- added coverage for nested filter structures and logical conditions  
- all tests passing (83 total)  

---

### 🛠 Internal

- removed EF Core tag from package metadata (moved to separate package strategy)  
- added missing XML documentation to resolve compiler warnings (CS1591)  



## [1.1.0]

### ✨ Features

- added dynamic projection engine using expression trees  
- enabled runtime field selection for optimized query results  
- support for nested property projection (e.g. `orders.customer.name`)  

---

### ⚡ Improvements

- merged nested select paths into a single navigation tree  
- eliminated duplicate joins for nested relations  
- optimized SQL generation to fetch only selected columns  
- replaced `Include` usage with projection-based query shaping  
- ensured compatibility with pagination and filtering  

---

### 🛠 Internal / CI

- use git tags as source of truth for package versioning  
- added pre-release versioning for feature branches (e.g. `2.0.0-dev.X`)  
- enabled NuGet publishing for feature branches and tagged releases  
- ensured proper tag fetching (`fetch-depth=0`) in CI pipeline  
- retained test coverage reporting in build workflow  

---

## [1.0.0]

### ✨ Features

- initial release of `DynamicQueryable.Extensions`
- basic filtering support using expression trees
- `IQueryable` extension methods for dynamic query building

---

### ⚡ Improvements

- EF Core–compatible expression generation

---

### 📦 Notes

- designed for dynamic query scenarios without OData