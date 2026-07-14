# SQL Dialects

## Overview

FlexQuery's Dapper provider supports six SQL dialects out of the box. Each dialect encapsulates all database-specific SQL generation concerns — identifier quoting, parameter syntax, pagination, boolean literals, and string concatenation — behind the `ISqlDialect` interface.

### Why Dialects Matter

SQL is not truly standard. `SELECT TOP 10` works in SQL Server but fails in PostgreSQL. `[Column]` is valid quoting in SQL Server but a syntax error in MySQL. `LIMIT/OFFSET` is PostgreSQL syntax but SQL Server uses `OFFSET/FETCH`. The dialect system ensures that FlexQuery generates correct SQL for your specific database without any manual intervention.

## Dialect Comparison

| Feature | SQL Server | PostgreSQL | MySQL | MariaDB | SQLite | Oracle |
|---------|------------|------------|-------|---------|--------|--------|
| **Quoting** | `[Column]` | `"Column"` | `` `Column` `` | `` `Column` `` | `"Column"` | `"Column"` |
| **Parameter prefix** | `@` | `:` | `?` | `?` | `@` | `:` |
| **Paging** | `OFFSET/FETCH` | `LIMIT/OFFSET` | `LIMIT/OFFSET` | `LIMIT/OFFSET` | `LIMIT/OFFSET` | `OFFSET/FETCH` |
| **Top-N** | `TOP (N)` | `LIMIT N` | `LIMIT N` | `LIMIT N` | `LIMIT N` | `FETCH FIRST N ROWS ONLY` |
| **Boolean TRUE** | `1` | `TRUE` | `TRUE` | `TRUE` | `1` | `1` |
| **Boolean FALSE** | `0` | `FALSE` | `FALSE` | `FALSE` | `0` | `0` |
| **Concatenation** | `+` | `\|\|` | `CONCAT()` | `CONCAT()` | `\|\|` | `\|\|` |
| **Min version** | 2012 | 9.5+ | 5.7+ | 10.2+ | 3.8+ | 12c |

## SQL Server

```csharp
opts.Dialect = new SqlServerDialect();
```

**Identifier quoting:** Square brackets `[Column]`

**Pagination:** Uses `OFFSET/FETCH` (requires SQL Server 2012+):
```sql
SELECT [Id], [Name] FROM [Users]
WHERE [Status] = @p0
ORDER BY [Name]
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
```

**Limitations:**
- `OFFSET/FETCH` requires an `ORDER BY` clause — FlexQuery automatically adds one if paging is requested without sorting
- Boolean columns use `1`/`0`, not `TRUE`/`FALSE`

## PostgreSQL

```csharp
opts.Dialect = new PostgreSqlDialect();
```

**Identifier quoting:** Double quotes `"Column"` — PostgreSQL is case-sensitive with quoted identifiers

**Pagination:** Standard `LIMIT/OFFSET`:
```sql
SELECT "Id", "Name" FROM "Users"
WHERE "Status" = :p0
ORDER BY "Name"
LIMIT :PageSize OFFSET :Offset
```

**Considerations:**
- PostgreSQL treats unquoted identifiers as lowercase. If your table has `CamelCase` columns, quoting is essential
- The `:` parameter prefix is used by Npgsql

## MySQL

```csharp
opts.Dialect = new MySqlDialect();
```

**Identifier quoting:** Backticks `` `Column` ``

**Pagination:** Standard `LIMIT/OFFSET`:
```sql
SELECT `Id`, `Name` FROM `Users`
WHERE `Status` = ?p0
ORDER BY `Name`
LIMIT ?PageSize OFFSET ?Offset
```

**String concatenation** uses `CONCAT()` function instead of operators:
```sql
CONCAT(`FirstName`, ' ', `LastName`)
```

## MariaDB

```csharp
opts.Dialect = new MariaDbDialect();
```

MariaDB is **not** a drop-in replacement for MySQL. While the quoting and parameter syntax are identical, MariaDB has its own versioning, features, and behaviors that can diverge. This dedicated dialect ensures correct SQL generation for MariaDB-specific edge cases.

**Behavior:** Identical to MySQL dialect in the current implementation, but exists as a separate class for future MariaDB-specific optimizations.

## SQLite

```csharp
opts.Dialect = new SqliteDialect();
```

**Identifier quoting:** Double quotes (ANSI SQL style)

SQLite is commonly used for:
- Integration testing
- Local development
- In-memory test databases
- Demo APIs

```sql
SELECT "Id", "Name" FROM "Users"
WHERE "Status" = @p0
ORDER BY "Name"
LIMIT @PageSize OFFSET @Offset
```

**Limitations:**
- No native BOOLEAN type — uses `1`/`0`
- Limited concurrent write support — not recommended for production multi-writer scenarios

## Oracle

```csharp
opts.Dialect = new OracleDialect();
```

**Identifier quoting:** Double quotes — Oracle uppercases unquoted identifiers by default

**Pagination:** Uses `OFFSET/FETCH` (requires Oracle 12c+):
```sql
SELECT "Id", "Name" FROM "Users"
WHERE "Status" = :p0
ORDER BY "Name"
OFFSET :Offset ROWS FETCH NEXT :PageSize ROWS ONLY
```

**Limitations:**
- Oracle 11g and earlier do not support `OFFSET/FETCH` — a `ROW_NUMBER()` based fallback may be needed
- No native BOOLEAN type in SQL — uses `1`/`0`
- The `:` parameter prefix is used by ODP.NET

## Automatic Dialect Resolution

If you don't want to hardcode a dialect, FlexQuery can auto-detect it from the `DbConnection` type:

```csharp
// The DefaultSqlDialectResolver inspects the connection type name
var dialect = DapperQueryOptions.GlobalDialectResolver.Resolve(connection);
```

The resolution priority is:
1. **Explicit `Dialect` property** on `DapperQueryOptions`
2. **`GlobalDefaultDialect`** static property
3. **`GlobalDialectResolver`** which inspects the connection type

## Custom Dialects

Implement `ISqlDialect` for unsupported databases:

```csharp
public class CockroachDbDialect : ISqlDialect
{
    public string ParameterPrefix => "$";
    public string GetCountExpression => "COUNT(1)";
    public string BooleanTrue => "TRUE";
    public string BooleanFalse => "FALSE";
    public char QuotePrefix => '"';
    public char QuoteSuffix => '"';

    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    public string GetPagingClause(string offsetParam, string limitParam)
        => $"LIMIT {limitParam} OFFSET {offsetParam}";

    public string GetLimitExpression(string limitParam)
        => $"LIMIT {limitParam}";

    public string Concatenate(params string[] parts)
        => string.Join(" || ", parts);

    public string CreateParameterName(string name) => $"${name}";
}
```

## Best Practices

1. **Match the dialect to your actual database** — Using `SqlServerDialect` against PostgreSQL will generate invalid SQL
2. **Set the dialect once globally** for single-database applications via `DapperQueryOptions.GlobalDefaultDialect`
3. **Use SQLite dialect for tests** — SQLite in-memory databases are fast and disposable
4. **Test pagination with each dialect** — OFFSET/FETCH vs LIMIT/OFFSET edge cases can surface in production

## Related Features

- [Getting Started](/providers/dapper/getting-started) — Installation and first query
- [SQL Generation](/providers/dapper/sql-generation) — How the translator uses dialects
