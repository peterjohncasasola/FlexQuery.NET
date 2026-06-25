# FlexQuery.NET.Parsers.Jql

[![NuGet Version](https://img.shields.io/nuget/v/FlexQuery.NET.Parsers.Jql.svg)](https://www.nuget.org/packages/FlexQuery.NET.Parsers.Jql)

JQL (Jira Query Language) parser for FlexQuery.NET.

## When to Use This Package

Install this package when you want to support JQL-style filter syntax in your API. The JQL parser integrates with the `QueryOptionsParser` pipeline so JQL queries are automatically detected and parsed alongside the native DSL format.

## Installation

```bash
dotnet add package FlexQuery.NET.Parsers.Jql
```

## Quick Start

```csharp
using FlexQuery.NET.Parsers.Jql;

var parser = new JqlQueryParser();
var filterGroup = parser.Parse("Status = 'Active' AND Age >= 18");

// GET /api/users?filter=Status = 'Active' AND Age >= 18
```

## JQL Syntax Examples

```jql
Status = 'Active'
Age >= 18 AND
Name CONTAINS 'john'
Status = 'Active' AND Age >= 18
Category = 'Electronics' OR Category = 'Books'
DeletedAt IS NULL
Status IN ('Active', 'Pending')
```

## Features

- **`JqlQueryParser`** — Parses JQL filter expressions into `FilterGroup` AST
- **Auto-Detection** — Registers as `IQueryParser` so JQL queries are handled seamlessly
- **Supported Operators** — eq, neq, gt, gte, lt, lte, contains, startswith, endswith, like, isnull, isnotnull, in, notin, between, any, all, count
- **Standalone Usage** — Can be used without the full FlexQuery execution pipeline

## Known Limitations

- The parser implements a subset of the full JQL specification — complex Jira functions and custom fields are not supported
- Date/time parsing uses .NET conventions rather than Jira-specific formats

## Related Packages

- [FlexQuery.NET](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET/README.md) — Core query engine
- [FlexQuery.NET.Parsers.MiniOData](https://github.com/peterjohncasasola/FlexQuery.NET/blob/main/src/FlexQuery.NET.Parsers.MiniOData/README.md) — Alternative parser for OData syntax

## Documentation

- [Query Formats Guide](https://flexquery.vercel.app/guide/query-formats)
- [Query Syntax Reference](https://flexquery.vercel.app/guide/query-syntax)
