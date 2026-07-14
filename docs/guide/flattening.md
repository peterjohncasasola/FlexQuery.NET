# Flattening

## Overview

FlexQuery.NET supports three projection modes. The **Flat** and **FlatMixed** modes reshape nested objects into flat key-value structures — useful for grid views, CSV exports, and analytics tools.

## Why this feature exists

Different consumers need different data shapes. A mobile application prefers clean, nested JSON objects. An older Kendo UI grid might expect a flat row dictionary where all fields are at the top level. A CSV export pipeline needs a completely flat structure. The `mode` parameter lets you serve all these consumers from a single endpoint without writing format-specific controllers.

---

## What It Does

The projection mode controls the **shape** of the JSON response:

- **Nested** (default): Preserves the object hierarchy
- **Flat**: Flattens all properties to top-level with dot-notation keys
- **FlatMixed**: Scalar navigations are flattened; collections remain nested

---

## When to Use

| Mode | Use When |
| :--- | :--- |
| `nested` | Standard API responses, mobile clients, structured consumption |
| `flat` | CSV export, spreadsheet tools, grid UI (Ag-Grid, DataTables) |
| `flat-mixed` | Hybrid — human-readable flat scalars, but preserving collection arrays |

---

## When NOT to Use

- Do not use `flat` when clients expect nested JSON (most REST clients).
- Do not use flat mode for deeply nested entities — the key names become very long.

---

## HTTP Examples

### Default (Nested)

```
GET /api/users?select=id,name,profile.bio,address.city
```

**Response:**
```json
{
  "id": 1,
  "name": "Alice",
  "profile": {
    "bio": "Software Engineer"
  },
  "address": {
    "city": "Singapore"
  }
}
```

### Flat Mode

```
GET /api/users?select=id,name,profile.bio,address.city&mode=flat
```

**Response:**
```json
{
  "id": 1,
  "name": "Alice",
  "profile.bio": "Software Engineer",
  "address.city": "Singapore"
}
```

### FlatMixed Mode

```
GET /api/users?select=id,name,profile.bio,orders&mode=flat-mixed
```

**Response:**
```json
{
  "id": 1,
  "name": "Alice",
  "profile_bio": "Software Engineer",
  "orders": [
    { "id": 101, "status": "shipped" }
  ]
}
```

---

## C# Examples

### Setting the Projection Mode in Code

```csharp
var options = new QueryOptions
{
    Select = new List<string> { "id", "name", "profile.bio" },
    ProjectionMode = ProjectionMode.Flat
};

var query = _context.Users.AsQueryable();
var projected = query.ApplySelect(options);
var data = await projected.ToListAsync();
```

### Parsing from Query String (Auto)

The `mode` query parameter is automatically parsed:

```
GET /api/users?select=id,name&mode=flat
```

Valid values: `Nested`, `Flat`, `FlatMixed` (case-insensitive when parsing from URL).

---

## Common Mistakes

### ❌ Using flat mode with deeply nested paths

```
GET /api/users?select=a.b.c.d.e&mode=flat
```

The key `a.b.c.d.e` becomes the flat key — hard to consume.

---

## Performance Notes

- Flat and FlatMixed modes add a key-normalization step on the result.
- For large result sets, this is negligible.
- The SQL query generated is identical regardless of projection mode — the mode only affects JSON serialization shape.
