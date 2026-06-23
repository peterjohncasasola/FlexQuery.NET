# Advanced Examples

Advanced patterns including nested filters, aggregates, projection modes, filtered includes, and aliasing.

---

## Example 1: Nested ANY Filter

**Scenario:** Get all users who have at least one shipped order with amount > 100.

**Request:**
```
GET /api/users?query=Orders.any(Status = "shipped" AND Amount > 100)&select=id,name&page=1&pageSize=20
```

**Controller:**
```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string>
        {
            "id", "name", "email", "orders.status", "orders.amount"
        };
        exec.MaxFieldDepth = 2;
    });

    return Ok(result);
}
```

**SQL Generated:**
```sql
SELECT u.Id, u.Name
FROM Users u
WHERE EXISTS (
  SELECT 1 FROM Orders o
  WHERE o.UserId = u.Id
    AND o.Status = 'shipped'
    AND o.Amount > 100
)
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
```

**Response:**
```json
{
  "data": [
    { "id": 1, "name": "Alice Chen" },
    { "id": 5, "name": "Carol White" }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 20
}
```

---

## Example 2: Aggregates with GROUP BY

**Scenario:** Dashboard showing total orders and revenue per status.

**Request:**
```
GET /api/orders?select=status,count(),sum(amount)&groupBy=status&sort=count():desc
```

**Response:**
```json
{
  "data": [
    { "status": "pending",   "allCount": 45, "amountSum": 12400.00 },
    { "status": "shipped",   "allCount": 38, "amountSum": 9800.50  },
    { "status": "cancelled", "allCount": 12, "amountSum": 3200.00  }
  ],
  "totalCount": 3
}
```

---

## Example 3: HAVING Condition

**Scenario:** Only show statuses where total revenue > 10000.

**Request:**
```
GET /api/orders?select=status,sum(amount)&groupBy=status&having=sum(amount):gt:10000
```

**Response:**
```json
{
  "data": [
    { "status": "pending", "amountSum": 12400.00 }
  ],
  "totalCount": 1
}
```

---

## Example 4: Filtered Includes

**Scenario:** Get all users, but only include their orders that are in "shipped" status.

**Request:**
```
GET /api/users?include=Orders(status:eq:shipped)&select=id,name&page=1&pageSize=3
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
        { "id": 108, "status": "shipped", "amount": 89.00  }
      ]
    },
    {
      "id": 2,
      "name": "Bob Smith",
      "orders": []
    },
    {
      "id": 3,
      "name": "Carol White",
      "orders": [
        { "id": 202, "status": "shipped", "amount": 450.00 }
      ]
    }
  ],
  "totalCount": 48,
  "page": 1,
  "pageSize": 3
}
```

Note: Bob has no shipped orders, but he is still included in results — the include filter only affects the related collection, not the root query.

---

## Example 5: Flat Projection Mode

**Scenario:** Export-ready flat response for a spreadsheet tool.

**Request:**
```
GET /api/users?select=id,name,profile.bio,address.city&mode=flat&page=1&pageSize=5
```

**Response:**
```json
{
  "data": [
    { "id": 1, "name": "Alice", "profile.bio": "Software Engineer", "address.city": "Singapore" },
    { "id": 2, "name": "Bob",   "profile.bio": "Product Manager",   "address.city": "London"    }
  ],
  "totalCount": 48,
  "page": 1,
  "pageSize": 5
}
```

---

## Example 6: Sort by Collection Aggregate

**Scenario:** Sort users by their total order count (most active users first).

**Request:**
```
GET /api/users?sort=orders.count():desc&select=id,name&page=1&pageSize=10
```

**Response:**
```json
{
  "data": [
    { "id": 7,  "name": "Power User" },
    { "id": 1,  "name": "Alice Chen" },
    { "id": 12, "name": "Bob Smith"  }
  ],
  "totalCount": 48,
  "page": 1,
  "pageSize": 10
}
```

---

## Example 7: Complex JSON Filter

**Scenario:** Complex nested condition using JSON format.

**Request:**
```
GET /api/users?filter={"logic":"and","filters":[
  {"field":"status","operator":"eq","value":"active"},
  {"logic":"or","filters":[
    {"field":"age","operator":"gte","value":"30"},
    {"field":"name","operator":"contains","value":"senior"}
  ]}
]}
```

Equivalent to: `status = "active" AND (age >= 30 OR name contains "senior")`

---

## Example 8: Role-Based Field Access

**Scenario:** Admin users see salary; regular users do not.

**Controller:**
```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.RoleAllowedFields = new Dictionary<string, HashSet<string>>
        {
            ["admin"]  = new HashSet<string> { "id", "name", "email", "salary", "status" },
            ["viewer"] = new HashSet<string> { "id", "name", "email", "status" }
        };

        exec.CurrentRole = User.IsInRole("admin") ? "admin" : "viewer";
        exec.MaxFieldDepth = 1;
    });

    return Ok(result);
}
```

**Admin request:**
```
GET /api/users?select=id,name,salary&filter=salary:gt:50000
```

**Viewer request (same endpoint, different role):**
```
GET /api/users?select=id,name,salary
```

**Viewer response (400):**
```json
{
  "errors": [
    {
      "field": "salary",
      "code": "FIELD_ACCESS_DENIED",
      "message": "Field 'salary' is not allowed for role 'viewer'."
    }
  ]
}
```

---

## Example 9: Manual Pipeline with Custom Pre-Filter

**Scenario:** Multi-tenant API where each request is scoped to a tenant.

```csharp
[HttpGet]
public async Task<IActionResult> GetOrders([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    // Parse
    var options = QueryOptionsParser.Parse(parameters);

    // Validate
    var execOptions = new QueryExecutionOptions
    {
        AllowedFields = new HashSet<string> { "id", "amount", "status", "createdAt" },
        MaxFieldDepth = 1
    };
    options.ValidateOrThrow<Order>(execOptions);

    // Start with tenant pre-filter (BEFORE FlexQuery filter)
    var query = _context.Orders
        .Where(o => o.TenantId == CurrentTenantId)
        .AsQueryable();

    // Apply FlexQuery pipeline
    query = query.ApplyFilter(options);
    query = query.ApplySort(options);

    var total = await query.CountAsync(ct);

    query = query.ApplyPaging(options);
    query = query.ApplyFilteredIncludes(options);

    var data = await query.ApplySelect(options).ToListAsync(ct);

    return Ok(options.BuildQueryResult(data, total));
}
```

---

## Example 10: DISTINCT Query

**Scenario:** Get distinct status values.

**Request:**
```
GET /api/users?select=status&distinct=true
```

**Response:**
```json
{
  "data": [
    { "status": "active"   },
    { "status": "inactive" },
    { "status": "pending"  },
    { "status": "banned"   }
  ],
  "totalCount": 4
}
```
