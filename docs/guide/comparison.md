# Comparison: FlexQuery.NET vs GraphQL vs OData

## Overview

This page provides an architectural comparison between FlexQuery.NET, GraphQL, and OData for dynamic querying in APIs. The goal is not to declare a universal "best" solution, but to highlight the different philosophies, strengths, and tradeoffs of each approach.

## Why this comparison exists

When engineering teams decide they need to provide frontends with dynamic filtering or projection, GraphQL and OData are typically the first two technologies considered. However, both represent massive architectural shifts. This guide helps architects evaluate if they truly need a graph-based paradigm (GraphQL), a heavy metadata standard (OData), or simply a robust query abstraction layer over standard REST (FlexQuery.NET).

---

## Philosophy

| | FlexQuery.NET | GraphQL | OData |
| :--- | :--- | :--- | :--- |
| **Protocol style** | REST | Graph-based API layer | REST + OASIS standard |
| **Client contract** | Query string / JSON params | Typed schema | OData metadata conventions |
| **Server complexity** | Low-Medium | High | Medium-High |
| **Client complexity** | Low | Medium | Medium |
| **Learning curve** | Low | High | Medium |
| **Payload style** | Minimal REST envelope | Nested graph responses | Metadata-driven REST |

---

## Different Design Goals

### FlexQuery.NET

FlexQuery.NET focuses on providing a flexible query layer on top of traditional REST APIs. 

It is designed for:
- dynamic filtering
- projection (SELECT)
- aggregates (SUM, MIN, MAX)
- field-level restrictions
- reusable query pipelines

**Typical use cases:**
- Traditional REST APIs
- Admin dashboards
- Reporting endpoints
- Multi-tenant systems
- Advanced search endpoints

---

### GraphQL

GraphQL focuses on client-driven data shaping through a strongly typed schema. Clients request exactly the fields they need through graph-based queries.

**Typical use cases:**
- Frontend-heavy applications (React/Apollo)
- Multi-client ecosystems (Mobile + Web pulling different shapes)
- Real-time subscription systems

---

### OData

OData focuses on standardized REST querying and interoperability. It is commonly used in Microsoft-centric ecosystems and enterprise tooling scenarios.

**Typical use cases:**
- Power BI integrations
- Excel integrations
- Azure Data Factory
- Enterprise interoperability

---

## The Same Query — Three Approaches

**Goal:**
- Users named "Alice" AND active status
- Sorted by creation date descending
- First 10 records
- Return exactly `id`, `name`, and `email`

---

### FlexQuery.NET

```http
GET /api/users
    ?filter=name:contains:alice%26status:eq:active
    &sort=createdAt:desc
    &page=1
    &pageSize=10
    &select=id,name,email
```

**Response**

```json
{
  "totalCount": 3,
  "resultCount": 3,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    {
      "id": 1,
      "name": "Alice Chen",
      "email": "alice@example.com"
    }
  ],
  "nextCursorToken": null
}
```

FlexQuery.NET keeps the API fully REST-compatible while adding dynamic querying capabilities with a standardized pagination envelope.

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

**Response**

```json
{
  "data": {
    "users": {
      "totalCount": 3,
      "items": [
        {
          "id": 1,
          "name": "Alice Chen",
          "email": "alice@example.com"
        }
      ]
    }
  }
}
```

GraphQL provides highly flexible client-driven data selection, but forces all operations (even reads) to go through `POST /graphql`, bypassing HTTP-level caching.

---

### OData

```http
GET /api/users
    ?$filter=contains(Name,'alice') and Status eq 'active'
    &$orderby=CreatedAt desc
    &$top=10
    &$skip=0
    &$select=Id,Name,Email
    &$count=true
```

**Response**

```json
{
  "@odata.context": "https://api.example.com/$metadata#Users(Id,Name,Email)",
  "@odata.count": 3,
  "value": [
    {
      "Id": 1,
      "Name": "Alice Chen",
      "Email": "alice@example.com"
    }
  ]
}
```

OData emphasizes standardized metadata-driven REST interoperability, but brings heavy URL syntax and metadata properties (`@odata.*`) into the payload.

---

## Nested Collection Query

**Goal:**
- Return users that have at least one order with the status "shipped"

---

### FlexQuery.NET (DSL)

```http
GET /api/users?filter=orders:any:status:eq:shipped
```

### FlexQuery.NET (FQL)

```http
GET /api/users?query=Orders.any(Status = "shipped")
```

### GraphQL

```graphql
query {
  users(
    where: {
      orders: {
        some: {
          status: {
            eq: "shipped"
          }
        }
      }
    }
  ) {
    items {
      id
      name
    }
  }
}
```

### OData

```http
GET /api/users?$filter=orders/any(o: o/Status eq 'shipped')
```

All three approaches support nested collection filtering, but with different query styles and ecosystem expectations.

---

## Response Payload Comparison

| | FlexQuery.NET | GraphQL | OData |
| :--- | :--- | :--- | :--- |
| **Primary data field** | `data` | nested graph structure | `value` |
| **Count field** | `totalCount` | schema-defined | `@odata.count` |
| **Metadata payload** | Minimal (Paging envelope) | Minimal | Heavy (Includes EDM context) |
| **Schema exposure** | None (Hidden) | Built-in Introspection | `$metadata` XML endpoint |

---

## Server Setup Comparison

### FlexQuery.NET

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync(parameters, exec =>
    {
        // Security policy enforced immediately inline
        exec.AllowedFields = ["id", "name", "email", "status"];
    });

    return Ok(result);
}
```

FlexQuery.NET is designed to integrate directly into existing ASP.NET Core REST APIs without altering your application's architecture.

---

### GraphQL (Hot Chocolate)

```csharp
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

GraphQL typically requires schema configuration, specialized resolvers, and GraphQL-aware clients (like Apollo).

---

### OData

```csharp
builder.Services
    .AddControllers()
    .AddOData(options =>
        options.Select()
               .Filter()
               .OrderBy()
               .Count()
               .SetMaxTop(100)
               .AddRouteComponents("odata", GetEdmModel()));

[EnableQuery]
[HttpGet]
public IQueryable<User> GetUsers()
{
    return _context.Users;
}
```

OData requires Entity Data Model (EDM) configuration and heavily modifies the global MVC output formatters.

---

## Feature Comparison

| Feature | FlexQuery.NET | GraphQL | OData |
| :--- | :---: | :---: | :---: |
| Dynamic filtering | ✅ | ✅ | ✅ |
| Sorting | ✅ | ✅ | ✅ |
| Paging | ✅ | ✅ | ✅ |
| Field projection | ✅ | ✅ | ✅ |
| Nested collection filters | ✅ | ✅ | ✅ |
| Aggregates | ✅ | ⚠️ Depends on implementation | ✅ |
| Filtered includes | ✅ | ✅ | ✅ |
| Field-level restrictions | ✅ Built-in | ⚠️ Resolver-based | ⚠️ Model-based |
| Validation pipeline | ✅ Built-in | ❌ Typically manual | ❌ Typically manual |
| REST compatibility | ✅ | ❌ | ✅ |
| OpenAPI / Swagger | ✅ | ❌ | ⚠️ Partial |
| Typed schema contract | ❌ | ✅ | ✅ Metadata |
| Learning curve | Low | High | Medium |
| Setup complexity | Low-Medium | High | Medium |

---

## Tradeoffs

### FlexQuery.NET
**Strengths**
- REST-native querying (Cachable GET requests).
- Unified, secure validation pipeline.
- Aggregates and projection work out-of-the-box.
- Minimal setup overhead.

**Tradeoffs**
- Smaller frontend tooling ecosystem compared to GraphQL/Apollo.
- Lacks a typed schema discovery mechanism.

---

### GraphQL
**Strengths**
- Highly flexible client-driven queries.
- Strong typed schema system with built-in introspection.
- Excellent frontend tooling ecosystem.

**Tradeoffs**
- Higher setup complexity and operational overhead.
- "N+1" loading problems require specific DataLoader strategies.
- Breaks standard HTTP caching semantics.

---

### OData
**Strengths**
- Standardized REST querying.
- Strong Microsoft ecosystem integration.
- Rich interoperability tooling (Excel, Power BI).

**Tradeoffs**
- Verbose query conventions.
- Extremely heavy XML/JSON metadata payloads.
- Steeper learning curve than traditional REST APIs.

---

## Choosing the Right Tool

| Scenario | Recommended Approach |
| :--- | :--- |
| Traditional REST APIs with advanced querying | **FlexQuery.NET** |
| Reporting APIs with dynamic grouping and aggregates | **FlexQuery.NET** |
| Frontend-heavy graph-driven applications | **GraphQL** |
| Real-time subscription systems | **GraphQL** |
| Enterprise Microsoft ecosystem integrations | **OData** |
| Power BI / Excel interoperability | **OData** |
