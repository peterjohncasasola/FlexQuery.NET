# Real-World Examples

Complete, production-ready examples showing FlexQuery.NET in real application contexts.

> **Note:** Examples using `FlexQueryAsync` require the `FlexQuery.NET.EntityFrameworkCore` or `FlexQuery.NET.Dapper` package. The core `FlexQuery.NET` package provides synchronous `FlexQuery` for advanced scenarios.

---

## Scenario 1: E-Commerce Order Dashboard

An admin dashboard needs to filter orders by status, date range, and customer — with sorting, pagination, and a count of matching records.

**Entity:**
```csharp
public class Order
{
    public int      Id            { get; set; }
    public int      CustomerId    { get; set; }
    public string   OrderNumber   { get; set; } = "";
    public decimal  Total         { get; set; }
    public DateTime OrderDate     { get; set; }
    public string   Status        { get; set; } = "";
    public Customer Customer      { get; set; } = null!;
}
```

**Controller:**
```csharp
[HttpGet("orders")]
public async Task<IActionResult> GetOrders([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    var result = await _context.Orders.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string>
        {
            "id", "orderNumber", "total", "status", "orderDate",
            "customer.name", "customer.email"
        };
        exec.FilterableFields  = new HashSet<string> { "status", "total", "orderDate", "customerId" };
        exec.SortableFields    = new HashSet<string> { "total", "orderDate", "status" };
        exec.SelectableFields  = new HashSet<string> { "id", "status", "total", "orderDate", "customer.name" };
        exec.BlockedFields     = new HashSet<string> { "customer.email" };
        exec.MaxFieldDepth     = 2;
    }, ct);

    return Ok(result);
}
```

**Sample Request — Orders in date range, sorted by total:**
```
GET /api/orders?filter=orderDate:between:2024-01-01,2024-12-31,status:in:pending,processing
              &sort=total:desc
              &page=1&pageSize=25
              &select=id,status,total,orderDate,customer.name
```

**Response:**
```json
{
  "totalCount": 143,
  "resultCount": 143,
  "page": 1,
  "pageSize": 25,
  "totalPages": 6,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    {
      "id": 1001,
      "status": "processing",
      "total": 1250.00,
      "orderDate": "2024-11-15T08:00:00Z",
      "customer": { "name": "Alice Chen" }
    },
    {
      "id": 998,
      "status": "pending",
      "total": 870.50,
      "orderDate": "2024-11-14T16:30:00Z",
      "customer": { "name": "Bob Smith" }
    }
  ],
  "nextCursorToken": null
}
```

---

## Scenario 2: Multi-Tenant SaaS Customer Management

A SaaS platform with admin and viewer roles — each role sees different fields.

**Controller:**
```csharp
[HttpGet("customers")]
public async Task<IActionResult> GetCustomers([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    var isAdmin = User.IsInRole("admin");

    var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
    {
        // Base fields available to all roles
        exec.AllowedFields = new HashSet<string>
        {
            "id", "name", "email", "status", "city"
        };

        // Role-specific additional fields
        exec.RoleAllowedFields = new Dictionary<string, HashSet<string>>
        {
            ["admin"] = new HashSet<string>
            {
                "id", "name", "email", "status", "city",
                "salary", "createdDate"
            },
            ["viewer"] = new HashSet<string>
            {
                "id", "name", "email", "status"
            }
        };

        exec.CurrentRole = isAdmin ? "admin" : "viewer";
        exec.BlockedFields = new HashSet<string> { "email" };
        exec.MaxFieldDepth = 2;

        // Scope to current tenant automatically
        // (tenant filtering is done via pre-filter below, not FlexQuery)
    }, ct);

    return Ok(result);
}
```

**Admin Request:**
```
GET /api/customers?filter=salary:gt:80000&select=id,name,salary,city&sort=salary:desc
```

**Viewer Request (same endpoint):**
```
GET /api/customers?filter=salary:gt:80000
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

## Scenario 3: Customer Catalog with Filtered Relations

**Entity:**
```csharp
public class Customer
{
    public int          Id          { get; set; }
    public string       Name        { get; set; } = "";
    public string       Email       { get; set; } = "";
    public string       City        { get; set; } = "";
    public string       Status      { get; set; } = "";
    public decimal      Salary      { get; set; }
    public DateTime     CreatedDate { get; set; }
    public List<Order>  Orders      { get; set; } = new();
}
```

**Request — Customers in New York, with recent orders only:**
```
GET /api/customers?filter=city:eq:New York,salary:gte:50000
                &include=Orders(status:eq:shipped)
                &sort=salary:desc
                &page=1&pageSize=10
                &select=id,name,email,city,orders.orderNumber,orders.total
```

**Response:**
```json
{
  "totalCount": 12,
  "resultCount": 12,
  "page": 1,
  "pageSize": 10,
  "totalPages": 2,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    {
      "id": 5,
      "name": "Carol White",
      "email": "carol@example.com",
      "city": "New York",
      "orders": [
        { "id": 501, "orderNumber": "ORD-501", "total": 299.99 },
        { "id": 508, "orderNumber": "ORD-508", "total": 149.50 }
      ]
    },
    {
      "id": 12,
      "name": "Dave Brown",
      "email": "dave@example.com",
      "city": "New York",
      "orders": []
    }
  ],
  "nextCursorToken": null
}
```

---

## Scenario 4: Analytics / Reporting Endpoint

**Scenario:** Monthly revenue report grouped by city and customer status.

**Controller:**
```csharp
[HttpGet("reports/revenue")]
[Authorize(Roles = "admin,analyst")]
public async Task<IActionResult> GetRevenueReport([FromQuery] FlexQueryParameters parameters, CancellationToken ct)
{
    var result = await _context.Customers.FlexQueryAsync(parameters, exec =>
    {
        exec.AllowedFields = new HashSet<string>
        {
            "city", "status", "salary", "createdDate"
        };
        exec.MaxFieldDepth = 1;
    }, ct);

    return Ok(result);
}
```

**Request:**
```
GET /api/reports/revenue?filter=createdDate:between:2024-01-01,2024-12-31,status:eq:active
                        &select=city,count(),sum(salary),avg(salary)
                        &groupBy=city
                        &having=sum(salary):gt:50000
                        &sort=sum(salary):desc
```

**Response:**
```json
{
  "totalCount": 3,
  "resultCount": 3,
  "page": 1,
  "pageSize": 20,
  "aggregates": null,
  "data": [
    { "city": "New York",  "allCount": 24, "salarySum": 180000.00, "salaryAvg": 7500.00 },
    { "city": "London",    "allCount": 18, "salarySum": 126000.00, "salaryAvg": 7000.00 },
    { "city": "Singapore", "allCount": 12, "salarySum": 72000.00,  "salaryAvg": 6000.00 }
  ],
  "nextCursorToken": null
}
```

---

## Scenario 5: Minimal API (No Controller)

```csharp
app.MapGet("/api/customers", async (
    [AsParameters] FlexQueryParameters parameters,
    AppDbContext db,
    CancellationToken ct) =>
{
    try
    {
        var result = await db.Customers.FlexQueryAsync<Customer>(parameters, exec =>
        {
            exec.AllowedFields = new HashSet<string> { "id", "name", "email", "city", "status" };
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
GET /api/customers?filter=city:eq:New York,status:eq:active&sort=salary:desc&page=1&pageSize=20
```
