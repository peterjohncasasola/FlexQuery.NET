# Comparison: FlexQuery.NET vs GraphQL vs OData

This page gives an honest, side-by-side comparison of FlexQuery.NET, GraphQL, and OData for dynamic querying in REST APIs.

---

## Philosophy

| | FlexQuery.NET | GraphQL | OData |
| :--- | :--- | :--- | :--- |
| **Protocol** | REST | Custom (HTTP POST) | REST + OASIS standard |
| **Client contract** | Query string / JSON params | Typed schema + SDL | OData metadata + URL conventions |
| **Server complexity** | Low | High | Medium-High |
| **Client complexity** | Low | Medium | Medium |
| **Payload verbosity** | Minimal | Wrapped (`data`, `errors`) | Verbose (`@odata.context`, metadata) |
| **Learning curve** | Low | High | Medium |

---

## The Same Query — Three Ways

**Goal:** Get users named "Alice" who are active, sorted by creation date, first 10, returning only id, name, email.

### FlexQuery.NET

```
GET /api/users?filter=name:contains:alice,status:eq:active&sort=createdAt:desc&page=1&pageSize=10&select=id,name,email
```

**Response:**
```json
{
  "data": [
    { "id": 1, "name": "Alice Chen", "email": "alice@example.com" }
  ],
  "totalCount": 3,
  "page": 1,
  "pageSize": 10
}
```

---

### GraphQL

```graphql
POST /graphql

query {
  users(
    where: {
      and: [
        { name: { contains: "alice" } }
        { status: { eq: "active" } }
      ]
    }
    order: [{ createdAt: DESC }]
    skip: 0
    take: 10
  ) {
    totalCount
    items {
      id
      name
      email
    }
  }
}
```

**Response:**
```json
{
  "data": {
    "users": {
      "totalCount": 3,
      "items": [
        { "id": 1, "name": "Alice Chen", "email": "alice@example.com" }
      ]
    }
  }
}
```

The response is **wrapped** in `data.users.items`. Every client must unwrap it.

---

### OData

```
GET /api/users?$filter=contains(Name,'alice') and Status eq 'active'
             &$orderby=CreatedAt desc
             &$top=10
             &$skip=0
             &$select=Id,Name,Email
             &$count=true
```

**Response:**
```json
{
  "@odata.context": "https://api.example.com/$metadata#Users(Id,Name,Email)",
  "@odata.count": 3,
  "value": [
    { "Id": 1, "Name": "Alice Chen", "Email": "alice@example.com" }
  ]
}
```

The response includes `@odata.context` metadata URL and uses `value` as the array key. Every client must handle the OData envelope.

---

## Nested Collection Query

**Goal:** Get users who have at least one shipped order.

### FlexQuery.NET (DSL)

```
GET /api/users?filter=orders:any:status:eq:shipped
```

### FlexQuery.NET (JQL)

```
GET /api/users?query=Orders.any(Status = "shipped")
```

### GraphQL

```graphql
query {
  users(where: { orders: { some: { status: { eq: "shipped" } } } }) {
    items { id name }
  }
}
```

### OData

```
GET /api/users?$filter=orders/any(o: o/Status eq 'shipped')
```

All three express the same query. FlexQuery.NET's DSL is the most concise.

---

## Response Payload Comparison

For the same data, the response payloads differ significantly:

| | FlexQuery.NET | GraphQL | OData |
| :--- | :--- | :--- | :--- |
| **Envelope key** | `data` | `data.entityName.items` | `value` |
| **Count field** | `totalCount` | `data.entityName.totalCount` | `@odata.count` |
| **Metadata** | None | None | `@odata.context` URL |
| **Extra wrappers** | 0 | 2 (`data`, type name) | 1 (`value`) + metadata |

FlexQuery.NET and GraphQL are comparable in response size. OData adds metadata overhead on every response.

---

## Server Setup Comparison

### FlexQuery.NET

```csharp
// 3 lines to a working dynamic API
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters) =>
    Ok(await _context.Users.FlexQueryAsync<User>(parameters, exec =>
        exec.AllowedFields = new HashSet<string> { "id", "name", "email", "status" }));
```

### GraphQL (Hot Chocolate)

```csharp
// Schema definition
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddFiltering()
    .AddSorting()
    .AddProjections();

public class Query
{
    [UseDbContext(typeof(AppDbContext))]
    [UseOffsetPaging]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<User> GetUsers([ScopedService] AppDbContext db) => db.Users;
}
```

Requires schema definition, resolver setup, and a separate GraphQL client on the frontend.

### OData

```csharp
// OData controller setup
builder.Services.AddControllers().AddOData(options =>
    options.Select().Filter().OrderBy().Count().SetMaxTop(100)
           .AddRouteComponents("odata", GetEdmModel()));

[EnableQuery]
[HttpGet]
public IQueryable<User> GetUsers() => _context.Users;
```

Requires EDM model definition and OData-specific client on the frontend. The `$metadata` endpoint exposes your full schema.

---

## Feature Comparison

| Feature | FlexQuery.NET | GraphQL | OData |
| :--- | :--- | :--- | :--- |
| Dynamic filtering | ✅ | ✅ | ✅ |
| Sorting | ✅ | ✅ | ✅ |
| Paging | ✅ | ✅ | ✅ |
| Field projection | ✅ | ✅ | ✅ |
| Nested collection filter | ✅ | ✅ | ✅ |
| Aggregates | ✅ | ✅ (partial) | ✅ |
| Filtered includes | ✅ | ✅ | ✅ ($expand with $filter) |
| Field-level security | ✅ Built-in | ⚠️ Resolver-level | ⚠️ EDM-level |
| Validation pipeline | ✅ Built-in | ❌ Manual | ❌ Manual |
| REST compatible | ✅ | ❌ (POST-based) | ✅ |
| OpenAPI/Swagger support | ✅ | ❌ | ⚠️ Partial |
| Schema exposure | ❌ (by design) | ✅ Introspection | ✅ $metadata |
| Learning curve | Low | High | Medium |
| Setup complexity | Low | High | Medium |

---

## When to Choose Each

### Choose FlexQuery.NET when:
- You are building a **REST API** and want to keep it REST.
- Your clients are consuming data, not building apps (mobile, dashboard, BI tools).
- You need **field-level security** without a separate authorization layer.
- You want minimal setup with maximum flexibility.
- You don't want clients to know your full schema.

### Choose GraphQL when:
- Your clients need **real-time subscriptions**.
- You have **many different client apps** with very different data needs (mobile, web, desktop).
- You want a **typed, introspectable schema** as the contract.
- Your team already knows GraphQL.

### Choose OData when:
- You are building for **Microsoft ecosystem** clients (Power BI, Excel, Azure Data Factory).
- You need **OData-compliant metadata** for enterprise interoperability.
- Your clients use OData-aware tools natively.
