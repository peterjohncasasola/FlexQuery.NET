# Include Filtering

Filtered Includes let you load related entity collections with inline `WHERE` conditions — without affecting the root query's results or count.

---

## What It Does

The include pipeline is **completely independent** from the WHERE pipeline.

- Loads navigation properties with optional inline filters
- Does not affect the root result count
- Uses EF Core `Include`/`ThenInclude` with `Where` on the collection

This means: "Give me all users, but only include their shipped orders."

The root query (users) is not filtered — you get all users. Only the included collection (orders) is filtered.

---

## When to Use

Use filtered includes when:

- You need to load related data alongside the root entity
- You want to filter which related records are included (e.g., only active orders)
- You want the client to control which relations are eager-loaded

---

## When NOT to Use

- Do **not** use filtered includes to filter the root entity. Use `filter` for that.
- Do **not** include deeply nested navigations without `MaxFieldDepth`.

---

## HTTP Examples

### Load Orders for Each User (No Filter)

```
GET /api/users?include=Orders
```

### Load Only Shipped Orders

```
GET /api/users?include=Orders(status:eq:shipped)
```

### Load Orders with Amount Above 100

```
GET /api/users?include=Orders(amount:gt:100)
```

### Multiple Includes

```
GET /api/users?include=Orders(status:eq:active),Profile
```

### Nested Include

```
GET /api/users?include=Orders(status:eq:active).Items
```

---

## C# Examples

### Applying Filtered Includes

```csharp
var options = QueryOptionsParser.Parse(parameters);

var query = _context.Users.AsQueryable();
query = query.ApplyFilter(options);
query = query.ApplySort(options);

var total = await query.CountAsync();

query = query.ApplyPaging(options);

// Apply include pipeline AFTER paging, BEFORE materialization
query = query.ApplyFilteredIncludes(options);

var data = await query.ToListAsync();
```

### Using FlexQueryAsync (Automatic)

`FlexQueryAsync` applies the include pipeline automatically:

```csharp
var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
{
    exec.AllowedFields = new HashSet<string> { "id", "name", "orders.*" };
});
```

---

## JSON Output Example

**Request:**
```
GET /api/users?include=Orders(status:eq:shipped)&select=id,name&page=1&pageSize=2
```

**Response:**
```json
{
  "data": [
    {
      "id": 1,
      "name": "Alice Chen",
      "orders": [
        { "id": 101, "status": "shipped", "amount": 250.00 },
        { "id": 105, "status": "shipped", "amount": 89.99 }
      ]
    },
    {
      "id": 2,
      "name": "Bob Smith",
      "orders": []
    }
  ],
  "totalCount": 48,
  "page": 1,
  "pageSize": 2
}
```

Note: Bob Smith has no shipped orders, so his `orders` array is empty — but he is still returned in the result.

---

## Important: Include vs. Filter

| | `filter` parameter | `include(filter)` |
| :--- | :--- | :--- |
| **Affects root query** | ✅ Yes | ❌ No |
| **Affects result count** | ✅ Yes | ❌ No |
| **Where condition applied** | Root entity | Related collection |
| **Use case** | Find users where any order is shipped | Get all users, include only their shipped orders |

**Example showing the difference:**

```
# Returns only users who HAVE a shipped order
GET /api/users?filter=orders:any:status:eq:shipped

# Returns ALL users, each with their shipped orders only
GET /api/users?include=Orders(status:eq:shipped)
```

---

## Common Mistakes

### ❌ Confusing Include filter with root filter

```
# This returns all users, with only their shipped orders included
GET /api/users?include=Orders(status:eq:shipped)

# This returns ONLY users who have at least one shipped order
GET /api/users?filter=orders:any:status:eq:shipped
```

Both are valid — just different use cases. Know which one you need.

### ❌ Calling ApplyFilteredIncludes after ToListAsync

```csharp
// WRONG — query already materialized
var data = await query.ToListAsync();
query = query.ApplyFilteredIncludes(options); // too late
```

```csharp
// CORRECT — apply includes before materialization
query = query.ApplyFilteredIncludes(options);
var data = await query.ToListAsync();
```

---

# Split Query Optimization

When including multiple collections (e.g., `?include=Orders,Profiles`), EF Core may generate a "cartesian explosion" where the result set grows exponentially.

To optimize this, you can enable **Split Queries** in the execution configuration:

```csharp
var result = await _context.Users.FlexQueryAsync(parameters, exec =>
{
    exec.UseSplitQuery = true;
});
```

This ensures that each collection is fetched via a separate SQL query, reducing the amount of redundant data transferred.

---

## Summary
