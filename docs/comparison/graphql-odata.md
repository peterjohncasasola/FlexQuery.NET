# FlexQuery.NET vs GraphQL & OData

A quick side-by-side comparison. For the full analysis with complete request/response examples, see the [Guide Comparison page](/guide/comparison).

---

## At a Glance

| | FlexQuery.NET | GraphQL | OData |
| :--- | :--- | :--- | :--- |
| Protocol | REST | Custom HTTP | REST + OASIS |
| Client contract | Query string params | Typed SDL schema | OData metadata |
| Response envelope | `{ data, totalCount }` | `{ data: { type: { items } } }` | `{ @odata.context, value }` |
| Field security built-in | ✅ | ❌ | ❌ |
| Validation built-in | ✅ | ❌ | ❌ |
| Schema exposed | ❌ | ✅ | ✅ |
| REST-compatible | ✅ | ❌ | ✅ |
| Setup complexity | Low | High | Medium |

---

## The Same Query

**Get active users named "alice", sorted by creation date, page 1:**

### FlexQuery.NET
```
GET /api/users?filter=name:contains:alice,status:eq:active&sort=createdAt:desc&page=1&pageSize=10&select=id,name,email
```

### GraphQL
```graphql
POST /graphql
{ users(where: { and: [{ name: { contains: "alice" } }, { status: { eq: "active" } }] }, order: [{ createdAt: DESC }], skip: 0, take: 10) { totalCount items { id name email } } }
```

### OData
```
GET /odata/Users?$filter=contains(Name,'alice') and Status eq 'active'&$orderby=CreatedAt desc&$top=10&$skip=0&$select=Id,Name,Email&$count=true
```

---

## Choose FlexQuery.NET when:
- You want to keep REST without POST-based queries
- You need field-level security with zero extra setup
- Your clients are data consumers, not app builders
- You don't want to expose your full schema

## Choose GraphQL when:
- You need real-time subscriptions
- You have many different clients (mobile, web, desktop) with diverging data needs
- A typed, introspectable schema is a hard requirement

## Choose OData when:
- You're building for Microsoft ecosystem tools (Power BI, Excel, Azure Data Factory)
- Enterprise interoperability through OData-compliant metadata is required

---

→ [See full comparison with request/response examples](/guide/comparison)
