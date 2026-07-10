# FlexQuery.NET.Parsers.Fql

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Parsers.Fql.svg)](https://www.nuget.org/packages/FlexQuery.NET.Parsers.Fql)

FQL (FlexQuery Language) parser for FlexQuery.NET.

## When to Use This Package

Install this package when you want to support Fql-style filter syntax in your API. The Fql parser integrates with the `QueryOptionsParser` pipeline so Fql queries are automatically detected and parsed alongside the native DSL format.

## Installation

```bash
dotnet add package FlexQuery.NET.Parsers.Fql
```

## Quick Start

```csharp
using FlexQuery.NET.Parsers.Fql;

var parser = new FqlQueryParser();
var filterGroup = parser.Parse("Status = 'Active' AND Age >= 18");

// GET /api/users?filter=Status = 'Active' AND Age >= 18
```

## Fql Syntax Examples

```Fql
Status = 'Active'
Age >= 18 AND
Name CONTAINS 'john'
Status = 'Active' AND Age >= 18
Category = 'Electronics' OR Category = 'Books'
DeletedAt IS NULL
Status IN ('Active', 'Pending')
```

## Features

- **`FqlQueryParser`** — Parses Fql filter expressions into `FilterGroup` AST
- **Auto-Detection** — Registers as `IQueryParser` so Fql queries are handled seamlessly
- **Supported Operators** — eq, neq, gt, gte, lt, lte, contains, startswith, endswith, like, isnull, isnotnull, in, notin, between, any, all, count
- **Standalone Usage** — Can be used without the full FlexQuery execution pipeline

## Known Limitations

- The parser implements a subset of the full FQL specification — complex functions and custom fields are not supported
- Date/time parsing uses .NET conventions

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.Parsers.MiniOData](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.MiniOData/README.md) — Alternative parser for OData syntax

## Documentation

- [Query Formats Guide](https://flexquery.vercel.app/guide/query-formats)
- [Query Syntax Reference](https://flexquery.vercel.app/guide/query-syntax)
