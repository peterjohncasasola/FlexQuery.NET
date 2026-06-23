# Real-World Examples

Complete, production-ready examples showing FlexQuery.NET in real application contexts.

---

## Scenario 1: E-Commerce Order Dashboard

An admin dashboard needs to filter orders by status, date range, and customer — with sorting, pagination, and a count of matching records.

**Entity:**
```csharp
public class Order
{
    public int      Id         { get; set; }
    public int      CustomerId { get; set; }
    public string   Status     { get; set; } = "";
    public decimal  Amount     { get; set; }
    public DateTime CreatedAt  { get; set; }
    public Customer Customer   { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = new();
}
```

**Controller:**
```csharp
[HttpGet("orders")]
public async Task<IActionResult> GetOrders([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    var result = await _context.Orders.FlexQueryAsync<Order>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string>
        {
            "id", "customerId", "status", "amount", "createdAt",
            "customer.name", "customer.email"
        };
        exec.FilterableFields  = new HashSet<string> { "status", "amount", "createdAt", "customerId" };
        exec.SortableFields    = new HashSet<string> { "amount", "createdAt", "status" };
        exec.SelectableFields  = new HashSet<string> { "id", "status", "amount", "createdAt", "customer.name" };
        exec.BlockedFields     = new HashSet<string> { "customer.passwordHash" };
        exec.MaxFieldDepth     = 2;
    }, ct);

    return Ok(result);
}
```

**Sample Request — Orders in date range, sorted by amount:**
```
GET /api/orders?filter=createdAt:between:2024-01-01,2024-12-31,status:in:pending,processing
              &sort=amount:desc
              &page=1&pageSize=25
              &select=id,status,amount,createdAt,customer.name
```

**Response:**
```json
{
  "data": [
    {
      "id": 1001,
      "status": "processing",
      "amount": 1250.00,
      "createdAt": "2024-11-15T08:00:00Z",
      "customer": { "name": "Alice Chen" }
    },
    {
      "id": 998,
      "status": "pending",
      "amount": 870.50,
      "createdAt": "2024-11-14T16:30:00Z",
      "customer": { "name": "Bob Smith" }
    }
  ],
  "totalCount": 143,
  "page": 1,
  "pageSize": 25
}
```

---

## Scenario 2: Multi-Tenant SaaS User Management

A SaaS platform with admin and viewer roles — each role sees different fields.

**Controller:**
```csharp
[HttpGet("users")]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    var isAdmin = User.IsInRole("admin");

    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        // Base fields available to all roles
        exec.AllowedFields = new HashSet<string>
        {
            "id", "name", "email", "status", "createdAt"
        };

        // Role-specific additional fields
        exec.RoleAllowedFields = new Dictionary<string, HashSet<string>>
        {
            ["admin"] = new HashSet<string>
            {
                "id", "name", "email", "status", "createdAt",
                "salary", "internalRating", "lastLoginAt", "tenantId"
            },
            ["viewer"] = new HashSet<string>
            {
                "id", "name", "email", "status"
            }
        };

        exec.CurrentRole = isAdmin ? "admin" : "viewer";
        exec.BlockedFields = new HashSet<string> { "passwordHash", "twoFactorSecret" };
        exec.MaxFieldDepth = 2;

        // Scope to current tenant automatically
        // (tenant filtering is done via pre-filter below, not FlexQuery)
    }, ct);

    return Ok(result);
}
```

**Admin Request:**
```
GET /api/users?filter=salary:gt:80000&select=id,name,salary,internalRating&sort=salary:desc
```

**Viewer Request (same endpoint):**
```
GET /api/users?filter=salary:gt:80000
```

**Viewer Response (400):**
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

## Scenario 3: Product Catalog with Filtered Relations

**Entity:**
```csharp
public class Product
{
    public int          Id         { get; set; }
    public string       Name       { get; set; } = "";
    public string       Category   { get; set; } = "";
    public decimal      Price      { get; set; }
    public List<Review> Reviews    { get; set; } = new();
    public List<Tag>    Tags       { get; set; } = new();
}
```

**Request — Products in electronics, with 5-star reviews only:**
```
GET /api/products?filter=category:eq:electronics,price:lte:500
                &include=Reviews(rating:eq:5)
                &sort=price:asc
                &page=1&pageSize=10
                &select=id,name,price,category
```

**Response:**
```json
{
  "data": [
    {
      "id": 5,
      "name": "Wireless Earbuds",
      "price": 89.99,
      "category": "electronics",
      "reviews": [
        { "id": 201, "rating": 5, "comment": "Amazing sound quality!" },
        { "id": 208, "rating": 5, "comment": "Best earbuds I've owned." }
      ]
    },
    {
      "id": 12,
      "name": "USB-C Hub",
      "price": 45.00,
      "category": "electronics",
      "reviews": []
    }
  ],
  "totalCount": 28,
  "page": 1,
  "pageSize": 10
}
```

---

## Scenario 4: Analytics / Reporting Endpoint

**Scenario:** Monthly revenue report grouped by region and product category.

**Controller:**
```csharp
[HttpGet("reports/revenue")]
[Authorize(Roles = "admin,analyst")]
public async Task<IActionResult> GetRevenueReport([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    var result = await _context.Orders.FlexQueryAsync<Order>(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string>
        {
            "status", "region", "category", "amount", "createdAt"
        };
        exec.MaxFieldDepth = 1;
    }, ct);

    return Ok(result);
}
```

**Request:**
```
GET /api/reports/revenue?filter=createdAt:between:2024-01-01,2024-12-31,status:eq:completed
                        &select=region,count(),sum(amount),avg(amount)
                        &groupBy=region
                        &having=sum(amount):gt:10000
                        &sort=sum(amount):desc
```

**Response:**
```json
{
  "data": [
    { "region": "North America", "allCount": 512, "amountSum": 128000.00, "amountAvg": 250.00 },
    { "region": "Europe",        "allCount": 380, "amountSum": 95000.00,  "amountAvg": 250.00 },
    { "region": "Asia Pacific",  "allCount": 210, "amountSum": 52500.00,  "amountAvg": 250.00 }
  ],
  "totalCount": 3
}
```

---

## Scenario 5: Minimal API (No Controller)

```csharp
app.MapGet("/api/products", async (
    [AsParameters] FlexQueryParameters parameters,
    AppDbContext db,
    CancellationToken ct) =>
{
    try
    {
        var result = await db.Products.FlexQueryAsync<Product>(parameters, exec =>
        {
            exec.AllowedFields = new HashSet<string> { "id", "name", "price", "category", "inStock" };
            exec.MaxFieldDepth = 1;
        }, ct);

        return Results.Ok(result);
    }
    catch (QueryValidationException ex)
    {
        return Results.BadRequest(new { errors = ex.ValidationResult.Errors });
    }
});
```

**Request:**
```
GET /api/products?filter=inStock:eq:true,price:between:10,100&sort=price:asc&page=1&pageSize=20
```
