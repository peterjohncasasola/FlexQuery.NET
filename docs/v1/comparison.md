> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# FlexQuery.NET vs GraphQL vs OData

## 💡 What is FlexQuery.NET?

**FlexQuery.NET is a lightweight query layer for REST APIs that enables
dynamic filtering, sorting, projection, and pagination --- without
introducing new protocols or architecture.**

It works directly on `IQueryable`, allowing queries to translate into
optimized SQL via EF Core.

``` http
GET /api/users?filter=Name:contains:John,Age:ge:18&sort=CreatedAt:desc
```

👉 No schema\
👉 No resolvers\
👉 No new endpoints

------------------------------------------------------------------------

## ⚡ Why Not Just Traditional REST?

Traditional REST APIs require backend changes for every new query
requirement:

-   New filters → new endpoints ❌\
-   New sorting → new code ❌\
-   New projections → DTO explosion ❌

### With FlexQuery.NET:

-   ✅ Dynamic filtering
-   ✅ Dynamic sorting
-   ✅ Dynamic projection
-   ✅ No backend changes

------------------------------------------------------------------------

## 📊 Quick Comparison

| Feature | FlexQuery.NET | GraphQL | OData |
| :--- | :--- | :--- | :--- |
| **Query Style** | REST query params (DSL/JSON) | Schema-based queries | `$filter`, `$select` |
| **Setup** | Minimal | High | High |
| **Learning Curve** | Low | Medium | High |
| **Backend Changes** | None | Required | Required |
| **Projection** | Yes | Yes | Yes |
| **Security** | Field-level control | Schema-based | Convention-based |
| **Performance** | Direct LINQ → SQL | Depends on resolvers | Depends on provider |

------------------------------------------------------------------------

## 🧠 Mental Model

| Technology | Approach |
| :--- | :--- |
| **GraphQL** | New API paradigm (schema-first) |
| **OData** | Standardized protocol |
| **FlexQuery.NET** | Enhances existing REST |

------------------------------------------------------------------------

## 🔥 Key Differences

### 🟦 FlexQuery.NET vs GraphQL

| Aspect | FlexQuery.NET | GraphQL |
| :--- | :--- | :--- |
| Schema Required | ❌ No | ✅ Yes |
| Setup | Minimal | Complex |
| Integration | Drop-in | New architecture |
| Flexibility | High | Very High |

👉 GraphQL provides powerful flexibility through schema-driven design,
but requires additional setup such as schema definition and resolver
implementation.\
👉 FlexQuery is simpler and integrates instantly.

------------------------------------------------------------------------

### 🟩 FlexQuery.NET vs OData

| Aspect | FlexQuery.NET | OData |
| :--- | :--- | :--- |
| Syntax | Clean DSL | Complex |
| Setup | Minimal | Heavy |
| Output | Clean JSON | Verbose metadata |
| Control | Full | Convention-driven |

👉 OData is powerful but complex.\
👉 FlexQuery is simpler and more flexible.

------------------------------------------------------------------------

## 📊 Side-by-Side Example

### Scenario

Get users where: - name contains "John" - include cancelled orders -
select specific fields

------------------------------------------------------------------------

### 🟦 FlexQuery.NET

``` http
GET /api/users?filter=Name:contains:John&select=Id,Name,Orders.Id,Orders.Status&include=Orders(Status = "cancelled")
```

``` json
{
  "data": [
    {
      "id": 1,
      "name": "John Doe",
      "orders": [
        {
          "id": 100,
          "status": "cancelled"
        }
      ]
    }
  ]
}
```

------------------------------------------------------------------------

### 🟨 GraphQL

``` graphql
{
  users(filter: { name_contains: "John" }) {
    id
    name
    orders(status: "cancelled") {
      id
      status
    }
  }
}
```

``` json
{
  "data": {
    "users": [
      {
        "id": 1,
        "name": "John Doe",
        "orders": [
          {
            "id": 100,
            "status": "cancelled"
          }
        ]
      }
    ]
  }
}
```

------------------------------------------------------------------------

### 🟥 OData

``` http
GET /api/users?$filter=contains(Name,'John')&$select=Id,Name&$expand=Orders($filter=Status eq 'cancelled')
```

``` json
{
  "@odata.context": "...",
  "value": [
    {
      "Id": 1,
      "Name": "John Doe",
      "Orders": []
    }
  ]
}
```

------------------------------------------------------------------------

## ⚙️ Architecture Comparison

| Technology | Architecture |
| :--- | :--- |
| GraphQL | Schema + resolvers |
| OData | Protocol + configuration |
| FlexQuery.NET | Direct IQueryable execution |

------------------------------------------------------------------------

## ⚡ Performance

| Technology | Behavior |
| :--- | :--- |
| FlexQuery.NET | Direct LINQ → SQL (EF optimized) |
| GraphQL | Depends on resolvers |
| OData | Depends on provider |

👉 FlexQuery avoids in-memory filtering and extra abstraction layers.

------------------------------------------------------------------------

## 💡 Developer Experience

| Concern | FlexQuery.NET | GraphQL | OData |
| :--- | :--- | :--- | :--- |
| Readability | ✅ Clean | ⚠️ Medium | ❌ Complex |
| Setup | ✅ Easy | ❌ Hard | ❌ Hard |
| Response | ✅ Clean JSON | ⚠️ Wrapped | ❌ Verbose |
| Learning Curve | ✅ Low | ⚠️ Medium | ❌ High |

------------------------------------------------------------------------

## 🎯 When to Choose Each

### ✅ Use FlexQuery.NET when:

-   You already have REST APIs
-   You want dynamic querying
-   You want minimal setup
-   You use .NET + EF Core
-   You want full control over validation

------------------------------------------------------------------------

### ✅ Use GraphQL when:

-   You need full client control
-   Multiple data sources
-   Schema-first design
-   Complex frontend needs

------------------------------------------------------------------------

### ✅ Use OData when:

-   Enterprise standards required
-   Existing OData ecosystem
-   Strict protocol compliance

------------------------------------------------------------------------

## 🚀 Final Positioning

FlexQuery.NET is:

-   **Lighter than GraphQL** → no schema, no resolvers\
-   **Simpler than OData** → no complex standards\
-   **More powerful than REST** → dynamic querying

------------------------------------------------------------------------

## 🧨 Key Takeaway

👉 Same result, different experience.

| Approach | Experience |
| :--- | :--- |
| GraphQL | Powerful but heavy |
| OData | Standard but complex |
| FlexQuery.NET | Simple + powerful |

------------------------------------------------------------------------

## 💬 One-Liner

> FlexQuery.NET gives you GraphQL-like flexibility inside your existing
> REST API --- without the complexity.

------------------------------------------------------------------------

## 🚀 Why It Matters

-   No new infrastructure
-   No schema maintenance
-   No boilerplate endpoints
-   Clean responses
-   Fast integration

