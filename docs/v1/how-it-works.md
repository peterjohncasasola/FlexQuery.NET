> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# How FlexQuery Works

FlexQuery.NET builds dynamic LINQ expression trees on top of `IQueryable`, allowing query parameters to be translated into efficient database queries.

## High-Level Flow

The pipeline transforms a high-level request into a precise database query:

**QueryRequest (API input)** →  
**QueryOptions (structured representation)** →  
**Expression Trees (LINQ predicates)** →  
**IQueryable (query pipeline)** →  
**SQL (executed by provider)**

---

# Step-by-Step Breakdown

### Step 1: Input
The process begins with a request, typically a query string (e.g., `?filter=age:gt:25`) or a DTO like `FlexQueryRequest`.

---

### Step 2: Parsing
The `QueryOptionsParser` converts raw input into a structured `QueryOptions` object.

This separates:
- client input (string/DTO)
- internal execution logic (typed model)

Think of `QueryOptions` as a structured representation of the query.

---

### Step 3: Expression Building
FlexQuery translates `QueryOptions` into C# Expression Trees.

For example, the filter `age:gt:25` is translated into a strongly-typed predicate:

```csharp
x => x.Age > 25`
```

### Step 4: Applying to IQueryable
FlexQuery applies the generated expressions to your `IQueryable` using standard LINQ methods (`.Where()`, `.OrderBy()`, `.Skip()`, etc.). 

```csharp
query = query.ApplyFilter(options);
query = query.ApplySort(options);
query = query.ApplyPaging(options);
```

**Important**: Applying these filters does **not** execute the query. FlexQuery simply extends the `IQueryable` chain, following standard LINQ deferred execution patterns.

### Step 5: Execution
The query is only executed when the `IQueryable` is materialized (e.g., calling `ToListAsync()`, `CountAsync()`, or using FlexQuery's `ToQueryResultAsync()`). At this point, the entire expression tree is sent to the database provider.

```csharp
var result = await query.ToListAsync();
```

---

## Performance & SQL Translation

FlexQuery is designed to execute logic at the database level. Because it works directly on `IQueryable`, it relies on your database provider (EF Core, Dapper, etc.) to translate expressions into SQL.

### Database vs. In-Memory
- **Efficient**: When applied to an `IQueryable` connected to a database, all filtering and paging happen in SQL.
- **Caution**: If you materialize the query (e.g., `.ToList()`) *before* applying FlexQuery, the filtering will happen in-memory on the application server. Always apply FlexQuery as early as possible in your query chain.

### Provider Dependency
The final SQL output depends on your LINQ provider's capabilities. While FlexQuery generates standard expressions, you should ensure your provider supports the specific operators or property types you are querying.

## Security & Validation

For public-facing APIs, security is handled through a separate validation pass. Using `ApplyValidatedQueryOptions` ensures:
- **Field Whitelisting**: Users can only query fields you explicitly allow.
- **Operator Safety**: Prevents usage of invalid or expensive operators on specific fields.
- **Malicious Input**: Protects against complex nesting or malformed expressions that could lead to "ReDoS" or SQL performance issues.

```csharp
var validation = QueryValidator.Validate<User>(options);
if (!validation.IsValid)
{
    return BadRequest(validation.Errors);
}
```

```csharp
query.ApplyValidatedQueryOptions(options);
```

## Key Takeaways

- **Expression-Based**: FlexQuery builds real C# expressions, not just string evaluations.
- **IQueryable-Native**: It integrates seamlessly with the standard LINQ pipeline.
- **Deferred Execution**: No database calls occur until you explicitly request the data.
- **Database-Level**: Filtering, sorting, and paging are offloaded to the database whenever possible.
- **Composable**: You can mix FlexQuery with your own hard-coded LINQ `.Where()` clauses.

> [!TIP]
> **Mental Model**: FlexQuery is a layer that builds dynamic LINQ expressions on top of `IQueryable`, ensuring your dynamic queries are as efficient as manual LINQ code.

