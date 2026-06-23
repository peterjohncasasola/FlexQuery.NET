# Projection

Projection lets clients control which fields are returned in the response. Instead of always returning the full entity, clients request only what they need.

---

## What It Does

Projection applies a dynamic `Select` to your `IQueryable`. It:

- Returns only the requested fields
- Reduces network payload and serialization cost
- Supports three output shapes: Nested, Flat, FlatMixed
- Handles nested paths (`Profile.Name`, `Address.City`)
- Supports aliasing
- Integrates with aggregates and GroupBy

---

## When to Use

Use projection when:

- Clients need different subsets of data from the same entity
- You want to reduce network payload on mobile or bandwidth-constrained clients
- You want a single endpoint that serves multiple UI views

---

## When NOT to Use

- Do not project without `SelectableFields` restrictions on a public API.
- Do not use projection as a substitute for proper access control — always validate allowed fields.

---

## HTTP Examples

### Basic Field Selection

```
GET /api/users?select=id,name,email
```

### Nested Path Selection

```
GET /api/users?select=id,name,profile.bio,address.city
```

### Wildcard (All Fields)

```
GET /api/users?select=*
```

### Projection Mode: Flat

```
GET /api/users?select=id,name,profile.bio&mode=flat
```

### Projection Mode: FlatMixed

```
GET /api/users?select=id,name,profile.bio&mode=flat-mixed
```

---

## Projection Modes

### Nested (Default)

Preserves the original object hierarchy:

```json
{
  "id": 1,
  "name": "Alice",
  "profile": {
    "bio": "Software Engineer",
    "location": "Singapore"
  }
}
```

### Flat

Flattens all properties to top-level with dot-notation keys:

```json
{
  "id": 1,
  "name": "Alice",
  "profile.bio": "Software Engineer",
  "profile.location": "Singapore"
}
```

### FlatMixed

Scalar navigation is flattened; collections remain nested:

```json
{
  "id": 1,
  "name": "Alice",
  "profile_bio": "Software Engineer",
  "profile_location": "Singapore"
}
```

---

## C# Examples

### Applying Projection

```csharp
var options = QueryOptionsParser.Parse(parameters);

var query = _context.Users.AsQueryable();
query = query.ApplyFilter(options);
query = query.ApplySort(options);
query = query.ApplyPaging(options);

// ApplySelect returns IQueryable<object>
var projected = query.ApplySelect(options);
var data = await projected.ToListAsync();
```

### Restricting Selectable Fields

```csharp
var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
{
    exec.SelectableFields = new HashSet<string>
    {
        "id", "name", "email", "profile.bio"
    };
});
```

### Projection with Aggregates

```
GET /api/users?select=status.count()&groupBy=status
```

```json
{
  "data": [
    { "status": "active",   "allCount": 42 },
    { "status": "inactive", "allCount": 6  }
  ]
}
```

---

## JSON Output Examples

**Request:**
```
GET /api/users?select=id,name,email&page=1&pageSize=3
```

**Response:**
```json
{
  "data": [
    { "id": 1, "name": "Alice Chen",  "email": "alice@example.com" },
    { "id": 2, "name": "Bob Smith",   "email": "bob@example.com" },
    { "id": 3, "name": "Carol White", "email": "carol@example.com" }
  ],
  "totalCount": 48,
  "page": 1,
  "pageSize": 3
}
```

**Nested path request:**
```
GET /api/users?select=id,name,address.city&mode=nested
```

```json
{
  "data": [
    { "id": 1, "name": "Alice", "address": { "city": "Singapore" } },
    { "id": 2, "name": "Bob",   "address": { "city": "London" } }
  ]
}
```

---

## Common Mistakes

### ❌ No SelectableFields restriction

```csharp
// WRONG — client could select passwordHash, internalNotes, etc.
var result = await _context.Users.FlexQueryAsync<User>(parameters);
```

### ❌ Requesting non-existent fields

```
GET /api/users?select=id,nonExistentField
```

Non-existent fields are silently skipped during projection. Validate fields first with `ValidateOrThrow<T>`.

### ❌ Using projection without sorting + paging

Projection after paging is the correct order. FlexQuery.NET handles this correctly when you use `FlexQueryAsync`. In a manual pipeline, always apply Select AFTER paging:

```csharp
// CORRECT ORDER
var filtered = ApplyFilter(query, options);
var sorted   = ApplySort(filtered, options);
var paged    = ApplyPaging(sorted, options);
var data     = await paged.ApplySelect(options).ToListAsync(); // Last
```

---

## Performance Notes

- Projection is compiled into expression trees — EF Core sends only requested columns to SQL.
- Without projection, all columns are fetched. On entities with many columns or large text fields, this is wasteful.
- `mode=flat` adds a small overhead for key normalization but is negligible.
- Avoid selecting collection navigation properties without `include` — they will be empty or null.
