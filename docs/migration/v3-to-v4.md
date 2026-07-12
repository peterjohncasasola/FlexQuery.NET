# Migrating from FlexQuery.NET 3.1.1 to 4.0

This guide walks through upgrading an existing FlexQuery.NET v3.1.1 application to v4. It focuses on the changes developers must make—organized by developer task rather than by individual API.

If you are starting fresh with FlexQuery.NET v4, see the [Getting Started](../guide/getting-started) documentation instead.

---

## Overview

**Who should read this guide:** Developers maintaining production applications built on FlexQuery.NET v3.1.1 who are upgrading to v4.

**Target frameworks:** .NET 6.0, .NET 8.0, .NET 10.0 (.NET 7.0 was removed).

**Upgrade effort:** Moderate. Most changes are mechanical—renames, namespace updates, and parameter replacements. Applications that use FQL syntax, aggregates, Dapper, or custom parsers require additional attention.

**What changed in v4:** The parser architecture was redesigned for deterministic behavior, aggregates moved to a typed system, provider options were separated from core options, several implementation details were internalized, and the public API surface was reduced to prevent misuse.

---

## Migration Impact

The upgrade effort depends on which FlexQuery.NET features your application uses:

| If your application uses... | Upgrade Impact |
| --------------------------- | -------------- |
| Native DSL only             | Low            |
| FQL                         | Medium         |
| EF Core                     | Medium         |
| Dapper                      | High           |
| ASP.NET Core                | Medium         |
| Custom Parsers              | High           |

Most changes are mechanical—renames, namespace updates, and parameter replacements. Applications combining multiple features (e.g., FQL + Dapper) will naturally have more migration surface area.

---

## Quick Migration

For the most common upgrade path—an application using default DSL syntax with EF Core:

1. **Update NuGet packages** to v4 across all projects.
2. **If using FQL:** Replace the `FlexQuery.NET.Parsers.Jql` package reference with `FlexQuery.NET.Parsers.Fql`.
3. **Register parser packages:** Call `Fql.Register()` or `MiniOData.Register()` during startup. Parsers are no longer auto-discovered.
4. **Replace removed APIs:** `QueryOptionsParser.Parse()` → `parameters.ToQueryOptions()`, `ApplyFilteredIncludes()` → `ApplyExpand()`, `QuerySyntax.Jql` → `QuerySyntax.Fql`.
5. **Rebuild and run tests.** Most compilation errors are resolved by steps 2–4.

The sections below explain each change in detail.

---

## Package Changes

Only one package name changed:

| Package | v3.1.1 | v4 |
|---|---|---|
| JQL FQL Parser | `FlexQuery.NET.Parsers.Jql` | `FlexQuery.NET.Parsers.Fql` |

All other packages—`FlexQuery.NET`, `FlexQuery.NET.EntityFrameworkCore`, `FlexQuery.NET.Dapper`, `FlexQuery.NET.AspNetCore`, `FlexQuery.NET.Parsers.MiniOData`, `FlexQuery.NET.Adapters.AgGrid`, `FlexQuery.NET.Adapters.Kendo`, and `FlexQuery.NET.Diagnostics`—retain their names. Only the version needs updating.

To update package references via CLI:
```bash
dotnet remove package FlexQuery.NET.Parsers.Jql
dotnet add package FlexQuery.NET.Parsers.Fql

# If referencing DI extensions from the Core package directly,
# remove the now-unnecessary using directives:
dotnet remove package FlexQuery.NET  # only if not needed elsewhere
```

---

## What's New in v4

### Parser Architecture

The parser system was redesigned for deterministic behavior. Parsers must be explicitly registered via a static `Register()` call (`Fql.Register()`, `MiniOData.Register()`) instead of being auto-discovered. The `QuerySyntax` enum was simplified to three values: `NativeDsl`, `Fql`, and `MiniOData`. The `IQueryParser` interface no longer includes `CanParse()`, making parser selection explicit and predictable.

### Keyset Pagination

Cursor-based pagination for large datasets. Use the `Cursor` and `UseKeysetPagination` properties on `FlexQueryBase`. Keyset pagination uses WHERE predicates instead of Skip/Take, providing significantly better performance for deep pages.

```csharp
var parameters = new FlexQueryParameters
{
    Filter = "status:eq:active",
    PageSize = 50,
    UseKeysetPagination = true,
    Cursor = previousResponse.NextCursorToken
};
```

### Typed Exception Hierarchy

A new exception hierarchy replaces the removed `InvalidFilterFieldException` and `InvalidSortFieldException`:

- `FlexQueryException` — abstract base for all FlexQuery errors.
- `QueryParseException` — thrown when a query parameter value is malformed. Exposes `ParameterName`, `Syntax`, and `ReceivedValue` properties.
- `ParserNotRegisteredException` — thrown when no parser has been registered for the configured `QuerySyntax`.

### Provider-Specific Options

EF Core and Dapper now use dedicated options types (`EfCoreQueryOptions`, `DapperQueryOptions`) instead of sharing `QueryExecutionOptions`. This keeps core options provider-agnostic while enabling provider-specific configuration such as `UseNoTracking` for EF Core.

### ASP.NET Core Enhancements

New `FlexQueryRequest` and `MiniODataRequest` models provide dedicated types for POST and MiniOData endpoints. All `ServiceCollectionExtensions` classes now use the `Microsoft.Extensions.DependencyInjection` namespace, making extension methods discoverable without explicit `using` directives.

### OpenAPI Integration

A new `FlexQuery.NET.OpenApi` package provides OpenAPI/Swagger support for FlexQuery endpoints.

### Execution APIs

Queries are executed through the existing `IQueryable<T>.FlexQuery(...)` . Global options are configured once at application startup through the Core package facade.

```csharp
// Configure global options once at startup (console app, background service, etc.)
FlexQueryCore.Configure(o => o.MaxPageSize = 500);


// Extension-method entry point
var result = db.Users.FlexQuery(queryOptions);
```

---

## Required Migration

### Parser System

The parser system was redesigned to be deterministic. Every change in this section requires a code update.

#### QueryOptionsParser Is No Longer Accessible

`QueryOptionsParser` was made `internal`. Its `Parse()` methods remain public within the assembly but are no longer accessible from application code.

**Before:**
```csharp
var options = QueryOptionsParser.Parse(parameters);
```

**After:**
```csharp
var options = parameters.ToQueryOptions();
```

The `ToQueryOptions()` extension method is available on `FlexQueryParameters`, `FlexQueryRequest`, and `MiniODataRequest`.

#### Parsers Must Be Registered Explicitly

v3.1.1 auto-discovered parser packages via reflection at startup. v4 requires explicit registration through a static `Register()` call. Without registration, requests targeting FQL or MiniOData syntax throw `ParserNotRegisteredException`.

**Before:**
```csharp
// No registration needed — parsers auto-discovered
services.AddFlexQuery();
```

**After:**
```csharp
// Configure global options and register parsers once at startup.
// No additional using statements needed — these are static methods on
// the parser classes in their own namespaces.
FlexQueryCore.Configure();
Fql.Register();       // Register the FQL parser
// or: MiniOData.Register(); // Register the MiniOData parser
```

#### IQueryParser.CanParse() Removed

The `CanParse()` method was removed from the `IQueryParser` interface. Custom parser implementations must delete this method.

**Migration:** Remove `CanParse()` from any class implementing `IQueryParser`. The interface now requires only `Syntax` and `Parse()`.

#### QuerySyntax Changes

`QuerySyntax.AutoDetect`, `QuerySyntax.Json`, and `QuerySyntax.Generic` were removed. Only `NativeDsl`, `Fql`, and `MiniOData` remain. `QuerySyntax.Jql` was renamed to `QuerySyntax.Fql`.

**Before:**
```csharp
options.QuerySyntax = QuerySyntax.Jql;
```

**After:**
```csharp
options.QuerySyntax = QuerySyntax.Fql;
```

The default syntax in v4 is `NativeDsl`. Applications that relied on `AutoDetect` must now set the syntax explicitly and register the corresponding parser.

#### Startup Registration Is Now Static (No DI for Core, Parsers, and Providers)

In v3.1.1, `FlexQuery.NET` and every provider/parser package exposed `IServiceCollection` extension methods (e.g. `AddFlexQuery()`, `AddFqlParser()`, `AddFlexQueryDapper()`) that performed initialization inside the DI container. In v4 these packages are framework-agnostic and no longer depend on `Microsoft.Extensions.DependencyInjection`. Initialization is performed with **static** methods called once at startup:

```csharp

FlexQueryCore.Configure(...)
FlexQueryEFCore.Configure(...)
FlexQueryDapper.Configure(...)

```
These static facades live in the root namespace of their respective packages.

| Package | v3.1.1 | v4 Static API |
|---------|--------|---------------|
| Core (`FlexQuery.NET`) | `services.AddFlexQuery()` | `FlexQueryCore.Configure(...)` |
| AspNetCore (`FlexQuery.NET.AspNetCore`) | `services.AddFlexQuery()` | `FlexQueryCore.Configure(...)` |
| FQL Parser | `services.AddFqlParser()` | `Fql.Register()` |
| MiniOData Parser | `services.AddMiniOData()` | `MiniOData.Register()` |
| EF Core | `services.AddFlexQueryEntityFrameworkCore()` | `FlexQueryEFCore.Configure(...)` |
| Dapper | `services.AddFlexQueryDapper(...)` | `FlexQueryDapper.Configure(...)` |

Only the ASP.NET Core and OpenAPI packages retain genuine DI extensions, because they integrate with the MVC / Swagger pipelines:

| Package | v4 DI Extension (namespace `Microsoft.Extensions.DependencyInjection`) |
|---------|--------------------------------------------------------------------------|
| ASP.NET Core | `AddFlexQuerySecurity(IMvcBuilder)` — registers the field-access filter |
| OpenAPI | `AddFlexQueryOpenApi(IServiceCollection)` — registers the OpenAPI transformer |

**Migration:** Remove old `using FlexQuery.NET.*.DependencyInjection` or `using FlexQuery.NET.*.Extensions` directives. Replace each `services.AddXxx(...)` initialization call with the corresponding static method. The ASP.NET Core `AddFlexQuerySecurity()` method lives in the `Microsoft.Extensions.DependencyInjection` namespace and is implicitly available in ASP.NET Core projects.

#### AddJqlParser Replaced by Fql.Register()

The `AddJqlParser()` / `AddFqlParser()` DI extension was removed. Registration is now a static call.

**Before:**
```csharp
services.AddJqlParser();
```

**After:**
```csharp
Fql.Register();
```

#### MiniOData Endpoints Must Use MiniODataRequest

MiniOData endpoints that previously accepted `FlexQueryParameters` must now use `MiniODataRequest`.

**Before:**
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
{
    var options = QueryOptionsParser.Parse(parameters, QuerySyntax.MiniOData);
    // ...
}
```

**After:**
```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] MiniODataRequest request)
{
    var options = request.ToQueryOptions();
    // ...
}
```

---

### Core Library

#### Core Package No Longer Registers DI Services

The `FlexQuery.NET` core package no longer exposes any `IServiceCollection` extension methods. Its `DependencyInjection` directory was removed and the dependency on `Microsoft.Extensions.DependencyInjection.Abstractions` was dropped, keeping the core framework-agnostic.

Global options are now configured once at startup with the static `FlexQueryCore.Configure()` method:

```csharp
// Configure global defaults — call once at startup, before executing queries
FlexQueryCore.Configure(o =>
{
    o.MaxPageSize = 500;
    o.QuerySyntax = QuerySyntax.Fql;
});
```

Non-ASP.NET Core applications (e.g., console apps or background services) should:
- Call `FlexQueryCore.Configure(...)` for global options
- Resolve queries via the `IQueryable<T>.FlexQuery(...)` extension methods

The `FlexQueryProcessor` implementation and its `IFlexQueryProcessor` contract are `internal` and are not part of the public API, so applications should not construct or reference them directly.

**Migration:** Remove `using FlexQuery.NET.DependencyInjection;` if present. Replace any `services.AddFlexQuery(...)` call with `FlexQueryCore.Configure(...)`.

#### FlexQueryParameters.Query Removed

The `Query` property was removed. Use `Filter` for all query string content.

**Migration:** Replace all references to `parameters.Query` with `parameters.Filter`.

#### Exception Handling

`InvalidFilterFieldException` and `InvalidSortFieldException` were removed. They are replaced by the new exception hierarchy:

| Exception | Purpose |
|---|---|
| `FlexQueryException` | Abstract base for all FlexQuery errors |
| `QueryParseException` | Parse failures (includes parameter name, syntax, and received value) |
| `ParserNotRegisteredException` | No parser registered for the configured syntax |

**Migration:**
```csharp
// Before
catch (InvalidFilterFieldException ex) { ... }
catch (InvalidSortFieldException ex) { ... }

// After
catch (QueryParseException ex) { ... }
catch (ParserNotRegisteredException ex) { ... }
catch (FlexQueryException ex) { ... }  // base for all FlexQuery errors
```

#### Public API Cleanup

Several implementation-detail APIs that were inadvertently exposed in v3 have been internalized or removed in v4. No migration path is provided for replicating the internal behavior—these were never guaranteed to remain stable.

| API | Status in v4 |
|---|---|
| `FlexQueryParameters.RawParameters` | Internal |
| `QueryOptions.CaseInsensitive` | Internal |
| `QueryOptions.EnableCache` | Internal |
| `QueryOptions.Items` | Internal |
| `QueryOptions.Skip` | Removed |
| `QueryOptions.Top` | Removed |
| `QueryOptions.GetCacheKey()` | Removed |
| `QueryOptions.Clone()` | Removed |
| `QueryOptions.CloneWithFilter()` | Removed |
| `QueryOptions.Ast` | Removed |
| `QueryExecutionOptions.UseNoTracking` | Removed (moved to `EfCoreQueryOptions`) |
| `QueryExecutionOptions.UseSplitQuery` | Removed (no v4 equivalent — split-query execution is no longer configurable) |
| `BaseQueryOptions.FieldAccessResolver` | Internal |
| `FlexQueryOptions.UseNoTracking` | Removed |

**Migration:** Remove all references to these members. If you were accessing `QueryOptions.CaseInsensitive` or `QueryOptions.Items`, consider whether the value can be configured through `FlexQueryOptions` or `BaseQueryOptions` instead.

---

### ASP.NET Core

#### DI Registration Namespaces Changed

The `ServiceCollectionExtensions` class for ASP.NET Core moved from `FlexQuery.NET.AspNetCore.Extensions` to `Microsoft.Extensions.DependencyInjection`. The only remaining DI extension is `AddFlexQuerySecurity()`, which registers the field-access filter with MVC. Global option configuration is no longer a DI concern — use the static `FlexQueryCore.Configure()` instead.

**Migration:** Remove `using FlexQuery.NET.AspNetCore.Extensions;`. The `Microsoft.Extensions.DependencyInjection` namespace is implicitly available in ASP.NET Core projects. Replace any `services.AddFlexQuery(...)` call with `FlexQueryCore.Configure(...)`.

#### POST Endpoints Must Use FlexQueryRequest

HTTP POST endpoints that previously accepted `FlexQueryParameters` must now use `FlexQueryRequest`. GET endpoints continue to use `FlexQueryParameters`.

**Before:**
```csharp
[HttpPost]
public async Task<IActionResult> Search([FromBody] FlexQueryParameters parameters)
{
    var options = QueryOptionsParser.Parse(parameters);
    // ...
}
```

**After:**
```csharp
[HttpPost]
public async Task<IActionResult> Search([FromBody] FlexQueryRequest request)
{
    var options = request.ToQueryOptions();
    // ...
}
```

---

### Aggregate System

#### AggregateFunction Enum Replaces Strings

`AggregateModel.Function` and `HavingCondition.Function` changed from `string` to the `AggregateFunction` enum. Code that assigns or compares string values will not compile.

**Before:**
```csharp
new AggregateModel { Function = "sum", Field = "Total" };
new HavingCondition { Function = "count", Operator = "gt", Value = "5" };

if (aggregate.Function == "sum") { ... }
```

**After:**
```csharp
new AggregateModel { Function = AggregateFunction.Sum, Field = "Total" };
new HavingCondition { Function = AggregateFunction.Count, Operator = "gt", Value = "5" };

if (aggregate.Function == AggregateFunction.Sum) { ... }
```

#### Aggregates Require a Dedicated Parameter

Aggregates embedded in the `select` parameter are no longer recognized. A dedicated `aggregates` query parameter is required.

**Before:**
```http
GET /api/orders?select=status,sum(amount),count(id)&groupBy=status
```

**After:**
```http
GET /api/orders?select=status&aggregates=amount:sum,id:count&groupBy=status
```

#### AggregateModel and HavingCondition Namespaces Changed

Both types moved from `FlexQuery.NET.Models` to `FlexQuery.NET.Models.Aggregates`. If your code references them with fully qualified names, update the namespace.

#### Aggregate Alias Format Changed

Auto-generated aggregate aliases changed from camelCase to PascalCase. Client code referencing aggregate results by alias must use the new casing (e.g., `AmountSum` instead of `amountSum`).

---

### Provider Integrations

#### EF Core Integration Improvements

##### Startup Registration Is Now Static

The `AddFlexQueryEntityFrameworkCore()` DI extension was removed. EF Core is now initialized once at startup with the static `FlexQueryEFCore.Configure(...)` method, which stores the global `FlexQueryEfCoreOptions` used by `FlexQueryAsync` and registers the EF Core-specific operators:

**Before:**
```csharp
services.AddFlexQueryEntityFrameworkCore();
```

**After:**
```csharp
FlexQueryEFCore.Configure(cfg =>
{
    cfg.UseNoTracking = true;
});
```

**Migration:** Remove the `AddFlexQueryEntityFrameworkCore()` call and the corresponding `using FlexQuery.NET.EntityFrameworkCore.DependencyInjection;` directive. Call `FlexQueryEFCore.Configure(...)` once during startup (or `FlexQueryEFCore.Setup()` if you only need operator registration).

##### ApplyFilteredIncludes Renamed to ApplyExpand

**Before:**
```csharp
query.ApplyFilteredIncludes(options);
```

**After:**
```csharp
query.ApplyExpand(options);
```

##### FlexQueryAsync Accepts EfCoreQueryOptions

The `configure` delegate parameter type changed from `QueryExecutionOptions` to `EfCoreQueryOptions`. The `configureExecution` parameter (used for event listeners) was removed. A `CancellationToken` parameter was added to all overloads.

**Before:**
```csharp
await query.FlexQueryAsync(parameters,
    configure: opts => opts.UseNoTracking = true,
    configureExecution: cfg => cfg.Listener = new MyListener());
```

**After:**
```csharp
await query.FlexQueryAsync(parameters,
    configure: opts => {
        opts.UseNoTracking = true;
        opts.Listener = new MyListener();
    });
```

If you were using `configureExecution` to attach an execution listener, set it via `BaseQueryOptions.Listener` instead:

```csharp
var efOptions = new EfCoreQueryOptions { Listener = new MyListener() };
await query.FlexQueryAsync(parameters, efOptions);
```

##### UseNoTracking Moved

The `UseNoTracking` property was removed from `QueryExecutionOptions` and is now available on `EfCoreQueryOptions`. Note that `QueryExecutionOptions.UseSplitQuery` was also removed in v4 and has no equivalent in `EfCoreQueryOptions` — split-query execution is no longer configurable.

**Before:**
```csharp
new QueryExecutionOptions {  UseNoTracking = false };
```

**After:**
```csharp
new EfCoreQueryOptions { UseNoTracking = false };
```

#### Dapper Integration Modernization

##### Startup Registration Is Now Static

The `AddFlexQueryDapper(...)` DI extension was removed. Dapper entity mappings are now configured once at startup with the static `FlexQueryDapper.Configure(...)` method, which builds the model and stores it internally as the global runtime model:

**Before:**
```csharp
services.AddFlexQueryDapper(cfg =>
{
    cfg.Model.Entity<Customer>().ToTable("Customers");
});
```

**After:**
```csharp
FlexQueryDapper.Configure(cfg =>
{
    cfg.Model.Entity<Customer>().ToTable("Customers");
});
```

**Migration:** Remove the `AddFlexQueryDapper(...)` call and the corresponding `using FlexQuery.NET.Dapper.DependencyInjection;` directive. Call `FlexQueryDapper.Configure(...)` once during startup. `FlexQueryAsync` automatically uses the configured global model; you no longer need to capture and re-apply the built model on each call.

##### DapperQueryOptions Namespace Changed

`DapperQueryOptions` moved from `FlexQuery.NET.Dapper` to `FlexQuery.NET.Dapper.Options`.

**Migration:** Update `using FlexQuery.NET.Dapper;` to `using FlexQuery.NET.Dapper.Options;` where `DapperQueryOptions` is referenced.

##### DapperQueryOptions API Simplified

The following members were removed. The class is now configuration-only:

- `Dialect`, `MappingRegistry`, `EntityType`, `Entity<T>()`, `ScanEntitiesFromAssembly()`
- `GlobalDefaultDialect`, `GlobalDialectResolver`
- `ToQueryExecutionOptions()`, the copy constructor
- `DapperQueryOptionsExtensions.UseSqlServer()`, `UsePostgreSql()`, `UseSqlite()`, `UseMappingRegistry()`

**Migration:** Remove references to these members. `DapperQueryOptions` now exposes only `CommandTimeout` (plus the internal `UseModel()` escape hatch for supplying a per-request model that overrides the global one). Entity metadata is configured once at startup via `FlexQueryDapper.Configure(...)`.

##### FlexQueryAsync Signature Changes

The `configureExecution` parameter (for event listeners) was removed. `CancellationToken` was added to all overloads. Two overloads were removed.

**Migration:** Remove `configureExecution` arguments. Add `CancellationToken` if needed.

---

### Adapters

#### AG Grid

`FromAgGridJson()` was replaced with `ToQueryOptions(JsonElement)`.

**Before:**
```csharp
var options = agGridJsonString.FromAgGridJson();
```

**After:**
```csharp
var options = agGridJsonElement.ToQueryOptions();
```

#### Kendo

`FromKendoJson()` was replaced with `ToQueryOptions(JsonElement)`.

**Before:**
```csharp
var options = kendoJsonString.FromKendoJson();
```

**After:**
```csharp
var options = kendoJsonElement.ToQueryOptions();
```

---

## Behavioral Changes

These changes affect runtime behavior without breaking compilation. Review each one.

#### Parser Auto-Detection Removed

v3.1.1 defaulted to `AutoDetect`, which tried registered parsers in order and fell back to DSL. v4 defaults to `NativeDsl` and only uses the parser registered for the configured syntax. Applications that relied on auto-detection for FQL or MiniOData queries will receive `ParserNotRegisteredException` at runtime.

**Review:** Ensure every endpoint that uses FQL or MiniOData syntax has `QuerySyntax` configured and the parser registered.

#### Strict Parameter Validation

v3.1.1 silently ignored malformed page numbers, sort expressions, and other parameter values. v4 throws `QueryParseException` with details about the invalid parameter, including the parameter name, expected syntax, and received value.

**Review:** Client code passing user-supplied query parameters should handle `QueryParseException` or validate parameters before sending.

#### Inline Aggregates in select No Longer Supported

v3.1.1 extracted aggregate expressions like `sum(amount)` from the `select` parameter. v4 treats everything in `select` as field names. Aggregate expressions must use the dedicated `aggregates` parameter.

**Review:** If your application embeds aggregates in `select`, move them to the `aggregates` parameter.

---

## Troubleshooting

#### `ParserNotRegisteredException` at runtime

**Cause:** No parser has been registered for the configured `QuerySyntax`. FQL or MiniOData queries will fail if the parser was not registered during startup.

**Fix:** Register the parser statically:
```csharp
Fql.Register();       // for FQL syntax
MiniOData.Register(); // for MiniOData syntax
```

#### `QueryParseException` when parsing parameters

**Cause:** A query parameter value is malformed—for example, a non-numeric page value or unbalanced filter parentheses.

**Fix:** Validate query strings before sending, or handle `QueryParseException` and return a descriptive error response. The exception exposes `ParameterName`, `Syntax`, and `ReceivedValue` for diagnostics.

#### `'QueryOptionsParser' is inaccessible due to its protection level`

**Cause:** The class was made `internal` in v4.

**Fix:** Replace `QueryOptionsParser.Parse(parameters)` with `parameters.ToQueryOptions()`.

#### `'QuerySyntax' does not contain a definition for 'Jql'`

**Cause:** The enum value was renamed.

**Fix:** Replace all references to `QuerySyntax.Jql` with `QuerySyntax.Fql`.

#### `Cannot implicitly convert type 'string' to 'AggregateFunction'` (or the namespaced equivalent)

**Cause:** `AggregateModel.Function` and `HavingCondition.Function` now use the `AggregateFunction` enum. The exact error shows the resolved namespace (e.g., `FlexQuery.NET.Models.Aggregates.AggregateFunction`).

**Fix:** Use enum members instead of string literals: `AggregateFunction.Sum` instead of `"sum"`.

#### `The name 'AddFlexQuery' does not exist in the current context`

**Cause:** The `FlexQuery.NET.DependencyInjection` namespace was removed from the Core package, and the `AddFlexQuery()` DI extension no longer exists. Global options are now configured with the static `FlexQueryCore.Configure()` method.

**Fix:** Call `FlexQueryCore.Configure(...)` once at startup. In ASP.NET Core projects, remove the old `using FlexQuery.NET.DependencyInjection;` directive — the `Microsoft.Extensions.DependencyInjection` namespace is implicitly available for the remaining `AddFlexQuerySecurity()` extension.

#### `'FqlParser' does not contain a definition for 'Register'` / no `AddFqlParser` available

**Cause:** The package reference still points to the old `FlexQuery.NET.Parsers.Jql` package, or code still calls the removed `AddFqlParser()` DI extension.

**Fix:** Update the package reference to `FlexQuery.NET.Parsers.Fql`. Replace `services.AddFqlParser()` with the static `Fql.Register()` call (namespace `FlexQuery.NET.Parsers.Fql`, no `using` needed if fully qualified or imported).

#### `'ApplyFilteredIncludes' does not exist in the current context`

**Cause:** The extension method was renamed.

**Fix:** Replace `ApplyFilteredIncludes()` with `ApplyExpand()`.

---

## Startup Methods Reference

v4 replaces the previous DI-based startup APIs with package-level static facades. Each package exposes its own startup entry point responsible for global configuration or one-time registration. All of them are framework-agnostic (no dependency on `Microsoft.Extensions.DependencyInjection`) and live in their own type namespaces, so they need no special `using` directive beyond the type itself.

### `FlexQueryCore.Configure(...)`

**Namespace:** `FlexQuery.NET` · **Package:** `FlexQuery.NET`

Configures the global `FlexQueryOptions` that serve as defaults for every query in the application. It:

- builds a fresh `FlexQueryOptions` instance,
- applies the optional `Action<FlexQueryOptions>` delegate you pass,
- sets the global parser syntax via `QueryOptionsParser.SetGlobalSyntax(options.QuerySyntax)`,
- stores the result as the process-wide default options (`FlexQueryCore.DefaultOptions`), which the `IQueryable<T>.FlexQuery(...)` extension methods use at execution time.

Call it **once** during startup, before any queries run:

```csharp
FlexQueryCore.Configure(o =>
{
    o.MaxPageSize = 500;
    o.MaxFieldDepth = 3;
    o.DefaultPageSize = 50;
    o.QuerySyntax = QuerySyntax.Fql;
});
```

### `Fql.Register()`

**Namespace:** `FlexQuery.NET.Parsers.Fql` · **Package:** `FlexQuery.NET.Parsers.Fql`

Registers the FQL query parser (`FqlQueryParser`) in the global parser registry under `QuerySyntax.Fql`. After this call, requests configured with FQL syntax can be parsed. Must be called **once** at startup if your application uses FQL. Replaces the old `AddJqlParser()` / `AddFqlParser()` DI extension.

```csharp
Fql.Register();
```

### `MiniOData.Register()`

**Namespace:** `FlexQuery.NET.Parsers.MiniOData` · **Package:** `FlexQuery.NET.Parsers.MiniOData`

Registers the Mini OData query parser (`MiniODataQueryParser`) in the global parser registry under `QuerySyntax.MiniOData`. After this call, requests configured with MiniOData syntax can be parsed. Must be called **once** at startup if your application uses MiniOData. Replaces the old `AddMiniOData()` / `AddFlexQueryMiniOData()` DI extension.

```csharp
MiniOData.Register();
```

### `FlexQueryEFCore.Configure(...)` and `FlexQueryEFCore.Setup()`

**Namespace:** `FlexQuery.NET.EntityFrameworkCore` · **Package:** `FlexQuery.NET.EntityFrameworkCore`

`FlexQueryEFCore.Configure(Action<FlexQueryEfCoreOptions>?)` configures the global EF Core execution options used by `FlexQueryAsync` at runtime and also ensures the EF Core-specific query operators are registered. It stores the supplied `FlexQueryEfCoreOptions` as the global default, so `FlexQueryAsync` automatically applies them when no per-execution options are provided. It replaces the old `AddFlexQueryEntityFrameworkCore()` DI extension.

`FlexQueryEFCore.Setup()` performs the same one-time operator registration without configuring options. Use it directly only if you do not need global EF Core options.

```csharp
FlexQueryEFCore.Configure(cfg =>
{
    cfg.UseNoTracking = true;
});
```

### `FlexQueryDapper.Configure(...)`

**Namespace:** `FlexQuery.NET.Dapper` · **Package:** `FlexQuery.NET.Dapper`

Configures the Dapper entity-mapping model from the supplied `Action<FlexQueryDapperOptions>` delegate and stores the resulting `FlexQueryModel` internally as the global runtime model. It:

- creates a `FlexQueryDapperOptions` instance,
- invokes your delegate so you can configure entity mappings (`o.Model.Entity<T>()` and relationships),
- calls `Model.Build()` to produce the immutable `FlexQueryModel`,
- stores that model as the process-wide default used by `FlexQueryAsync` at execution time.

`FlexQueryModel` itself is an internal implementation detail and is not returned. This replaces the old `AddFlexQueryDapper(...)` DI extension (including the `AddFlexQueryDapperSqlServer/PostgreSql/Sqlite` dialect variants). Must be called **once** at startup if your application uses Dapper.

```csharp
FlexQueryDapper.Configure(o =>
{
    o.Model.Entity<Customer>().ToTable("Customers");
    o.Model.Entity<Order>().ToTable("Orders");
});
```

---

## FAQ

#### Why was JQL renamed to FQL?

"FQL" (FlexQuery Language) better reflects that this is the project's native query language, not a Jira Query Language compatibility layer. The rename aligns naming across the ecosystem.

#### Why is parser registration explicit?

v3 auto-discovery caused ambiguity when multiple parsers could handle the same input. Explicit registration makes parser selection predictable, documents the dependency clearly, and eliminates runtime surprises.

#### Why was automatic syntax detection removed?

`CanParse()` was unreliable and ambiguous—multiple parsers could return `true` for the same input, and the first match wasn't always the correct one. Explicit `QuerySyntax` selection removes ambiguity and makes request processing deterministic.

#### Why are aggregates separate from `select`?

Mixing projection fields with aggregate functions in `select` (e.g., `select=status,sum(amount)`) created ambiguity. The dedicated `aggregates` parameter cleanly separates concerns, matching how SQL dialects and reporting tools model the distinction.

#### Why use AggregateFunction instead of strings?

String-typed function names allowed invalid values at compile time. The enum provides type safety, IntelliSense, and eliminates runtime errors from typos.

#### Why were many APIs made internal?

Properties like `CaseInsensitive`, `Items`, and `Clone()` were implementation details exposed inadvertently. Internalizing them reduces the API surface, prevents misuse, and allows the team to evolve the internal implementation without breaking changes.

#### Why was DI registration removed from the Core, parser, and provider packages?

In v3 most packages exposed `IServiceCollection` extension methods (`AddFlexQuery`, `AddJqlParser`, `AddMiniOData`, `AddFlexQueryDapper`, `AddFlexQueryEntityFrameworkCore`, etc.). Auditing v3.1.1 showed that the majority of these were *fake* registrations: the services they added were never actually resolved from the container. For example, parsers were discovered through a static registry / reflection auto-discovery rather than DI, and the Dapper options were consumed through a static `GlobalDefaultDialect` property rather than an injected `DapperQueryOptions`. The DI registrations added packages, public surface area, and a misleading "this is wired into the container" signal without providing any real benefit.

In v4 the Core, parser (FQL/MiniOData), EF Core, and Dapper packages are framework-agnostic and no longer depend on `Microsoft.Extensions.DependencyInjection.Abstractions`. Initialization is performed with explicit **static** methods called once at startup — `FlexQueryCore.Configure()`, `Fql.Register()`, `MiniOData.Register()`, `FlexQueryEFCore.Setup()`, `FlexQueryDapper.Configure(...)` — which makes registration deterministic and removes the dead DI surface. Only the packages that genuinely integrate with the ASP.NET Core pipeline keep DI extensions: `AddFlexQuerySecurity()` (registers an MVC filter) and `AddFlexQueryOpenApi()` (registers the OpenAPI transformer).

#### Why did the DI extension namespaces change?

The old `ServiceCollectionExtensions` classes lived in package-specific namespaces such as `FlexQuery.NET.DependencyInjection`, `FlexQuery.NET.AspNetCore.Extensions`, `FlexQuery.NET.Parsers.Jql`, `FlexQuery.NET.Parsers.MiniOData.Extensions`, `FlexQuery.NET.EntityFrameworkCore.DependencyInjection`, and `FlexQuery.NET.Dapper.DependencyInjection`. Because each package used its own namespace, callers had to add a different `using` directive per package — a divergence from how the wider .NET ecosystem works, where DI extensions are conventionally placed in `Microsoft.Extensions.DependencyInjection` so they are discoverable without explicit `using` statements.

Since v4 dropped DI from the Core, parser, EF Core, and Dapper packages entirely, those namespaces are gone. The remaining DI extensions — `AddFlexQuerySecurity()` (ASP.NET Core) and `AddFlexQueryOpenApi()` (OpenAPI) — were consolidated into the `Microsoft.Extensions.DependencyInjection` namespace. With those `using` directives removed, the static startup methods (e.g. `Fql.Register()`, `FlexQueryEFCore.Configure()`) are reached through their own type namespaces and no longer require DI namespace imports.


#### Can v3 and v4 coexist?

No. Both versions cannot be referenced in the same project. Upgrade all FlexQuery.NET packages to v4 simultaneously.

#### Can I continue using DSL after upgrading?

Yes. DSL (default syntax) remains fully supported and unchanged. The only required change is replacing `QueryOptionsParser.Parse(parameters)` with `parameters.ToQueryOptions()`.

---

## Upgrade Checklist

- [ ] All FlexQuery.NET packages updated to v4
- [ ] `FlexQuery.NET.Parsers.Jql` → `FlexQuery.NET.Parsers.Fql` (if using FQL)
- [ ] `FlexQueryCore.Configure(...)` called once at startup for global options
- [ ] Parser packages registered via `Fql.Register()` / `MiniOData.Register()`
- [ ] EF Core: `FlexQueryEFCore.Configure(...)` called at startup (replaces `AddFlexQueryEntityFrameworkCore()`)
- [ ] Dapper: `FlexQueryDapper.Configure(...)` called at startup (replaces `AddFlexQueryDapper()`)
- [ ] `using FlexQuery.NET.DependencyInjection;` removed (Core is DI-free; use static `FlexQueryCore.Configure()`)
- [ ] `using FlexQuery.NET.AspNetCore.Extensions;` removed (now `Microsoft.Extensions.DependencyInjection`)
- [ ] `using FlexQuery.NET.Parsers.Fql.DependencyInjection;` removed (use static `Fql.Register()`)
- [ ] `using FlexQuery.NET.Parsers.MiniOData.DependencyInjection;` / `.Extensions` removed (use static `MiniOData.Register()`)
- [ ] `using FlexQuery.NET.EntityFrameworkCore.DependencyInjection;` removed (use static `FlexQueryEFCore.Configure()`)
- [ ] `using FlexQuery.NET.Dapper.DependencyInjection;` removed (use static `FlexQueryDapper.Configure()`)
- [ ] `QueryOptionsParser.Parse()` → `parameters.ToQueryOptions()`
- [ ] `ApplyFilteredIncludes()` → `ApplyExpand()`
- [ ] `QuerySyntax.Jql` → `QuerySyntax.Fql`
- [ ] POST endpoints use `FlexQueryRequest`, MiniOData endpoints use `MiniODataRequest`
- [ ] Aggregates moved from `select` to `aggregates` parameter
- [ ] `AggregateModel.Function` / `HavingCondition.Function` use `AggregateFunction` enum
- [ ] Catch blocks updated for new exception types
- [ ] DapperQueryOptions namespace updated (if using Dapper)
- [ ] Project builds without errors
- [ ] All tests pass
