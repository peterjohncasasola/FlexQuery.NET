# FlexQuery.NET.Parsers.Fql

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Parsers.Fql.svg)](https://www.nuget.org/packages/FlexQuery.NET.Parsers.Fql)

FQL (FlexQuery Language) parser for FlexQuery.NET — a SQL-inspired query syntax.

## When to Use This Package

Install this package when you want to support FQL-style filter syntax in your API — a readable, SQL-inspired alternative to the native DSL. After registration, clients send FQL expressions in the `filter` (and related) parameters and they are parsed through the standard FlexQuery pipeline.

## Installation

```bash
dotnet add package FlexQuery.NET.Parsers.Fql
```

## Registration

Call `Fql.Register()` once at startup, then select the FQL syntax globally or per request:

```csharp
using FlexQuery.NET;
using FlexQuery.NET.Parsers.Fql;

Fql.Register();   // registers the FQL parser in the global parser registry

// Option A: set FQL as the application-wide default syntax
FlexQueryCore.Configure(options =>
{
    options.DefaultQuerySyntax = QuerySyntax.Fql;
});

// Option B: choose FQL per request
options.QuerySyntax = QuerySyntax.Fql;
```

## Quick Start

```http
GET /api/users?filter=Status = 'Active' AND Age >= 18&sort=Name asc&page=1&pageSize=20
```

## FQL Syntax Examples

```text
Status = 'Active'
Age >= 18
Status = 'Active' AND Age >= 18
Category = 'Electronics' OR Category = 'Books'
Name CONTAINS 'john'
Status IN ('Active', 'Pending')
DeletedAt IS NULL
Age BETWEEN 18 AND 30
```

## Features

- **Full Parameter Support** — Parses `filter`, `select`, `sort`, `aggregate`, and `having` expressions into the FlexQuery model
- **Supported Operators** — `=`, `>=`, `>`, `<=`, `<`, `!=`, plus `IN`, `NOT IN`, `CONTAINS`, `STARTSWITH`, `ENDSWITH`, `LIKE`, `IS NULL`, `BETWEEN`, and collection quantifiers (`ANY`, `ALL`, `COUNT`)
- **Familiar Keywords** — `AND`, `OR`, `ASC`, `DESC`, aggregate functions `SUM`, `AVG`, `MIN`, `MAX`, `COUNT`, and `AS` aliases
- **Integration** — Registers into the global parser registry so FQL queries flow through the same validation and execution pipeline

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.Parsers.MiniOData](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.MiniOData/README.md) — Alternative parser for OData syntax

## Documentation

- [Query Syntax](https://flexquery.vercel.app/docs/concepts/query-syntax)
