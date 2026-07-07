using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexQuery.NET.Dapper.DependencyInjection;
using FlexQuery.NET.EntityFrameworkCore.DependencyInjection;
using FlexQuery.NET.Samples.WebApi.Data;
using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Add SQLite Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=flexquery_sample.db"));

// 3. Add Controllers and Configure JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

//Global Config
builder.Services.AddFlexQueryDapper(cfg =>
{
    cfg.UseSqlite();
    cfg.Model.Entity<Customer>()
        .ToTable("Customers")
        .HasMany(c => c.Orders).WithForeignKey("CustomerId");
    cfg.Model.Entity<Order>()
        .ToTable("Orders");
});

builder.Services.AddFlexQueryEntityFrameworkCore();

// 4. Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FlexQuery.NET Sample API",
        Version = "v1",
        Description = """
# FlexQuery.NET Sample API

A sample Web API demonstrating **FlexQuery.NET** with multiple data providers and frontend integrations.

FlexQuery.NET provides a unified query pipeline for building powerful, flexible APIs with support for filtering, sorting, paging, field selection, and relationship loading.

---

## Supported Providers

- Entity Framework Core
- Dapper

---

## Supported Features

| Feature | Supported |
|----------|:---------:|
| Filtering | ✅ |
| Multi-column Sorting | ✅ |
| Pagination | ✅ |
| Field Selection (`select`) | ✅ |
| Relationship Loading (`include`) | ✅ |
| Total Count | ✅ |

---

## Query Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `filter` | Filter expression | `Status:eq:Active` |
| `sort` | Sort expression | `LastName:asc` |
| `page` | 1-based page number | `1` |
| `pageSize` | Number of records per page | `20` |
| `select` | Comma-separated fields to return | `Id,FirstName,Email` |
| `include` | Related entities to load | `Orders` |

---

## Example Requests

### Filter

```http
GET /api/customers?filter=City:eq:'New York'
```

### Sort

```http
GET /api/customers?sort=LastName:asc
```

### Pagination

```http
GET /api/customers?page=2&pageSize=25
```

### Field Selection

```http
GET /api/customers?select=Id,FirstName,Email
```

### Include Relationships

```http
GET /api/customers?include=Orders
```

### Combined Query

```http
GET /api/customers?filter=Status:eq:'Active'&sort=LastName:asc&page=1&pageSize=20&include=Orders
```

---

## Sample Response

```json
{
  "data": [
    {
      "id": 1,
      "firstName": "John",
      "lastName": "Doe"
    }
  ],
  "totalCount": 125,
  "page": 1,
  "pageSize": 20
}
```

---

## Interactive Samples

| Demo | URL |
|------|-----|
| Landing Page | http://localhost:5000/ |
| FlexQuery Playground | http://localhost:5000/playground.html |
| AG Grid (SSRM) | http://localhost:5000/aggrid.html |
| Kendo UI Grid | http://localhost:5000/kendo.html |
| Provider Comparison | http://localhost:5000/comparison.html |

---

## Notes

- No authentication is required.
- The SQLite sample database is automatically seeded on application startup.
- All sample endpoints support the same FlexQuery query syntax.
- Works consistently across Entity Framework Core and Dapper.
""",
        Contact = new OpenApiContact
        {
            Name = "FlexQuery.NET",
            Url = new Uri("https://github.com/peterjohncasasola/FlexQuery.NET")
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 5. Seed SQLite Database on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.InitializeAsync(dbContext);
}

// 6. Middleware Pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "FlexQuery.NET Samples v1");
    options.RoutePrefix = "swagger"; // Serve Swagger at /swagger
    options.DocumentTitle = "FlexQuery.NET Swagger UI";
});

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles(); // Serve aggrid.html and kendo.html from wwwroot
app.MapControllers();

app.Run();
