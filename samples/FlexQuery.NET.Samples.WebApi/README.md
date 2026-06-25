# FlexQuery.NET Web API Sample Application

A unified, lightweight demonstration of **FlexQuery.NET** showcasing how to parse, validate, and execute dynamic queries across EF Core, Dapper, AG Grid, and Kendo UI.

This project is built as a developer onboarding resource under the **5-minute understanding rule**. It uses an intentionally flat, simple architecture (no Clean Architecture, CQRS, Repository Pattern, or layers) to showcase how FlexQuery.NET integrates directly into standard .NET Controllers.

---

## What is FlexQuery.NET?

**FlexQuery.NET** is an open-source library that provides a unified query syntax across multiple data access technologies:
- **Entity Framework Core**
- **Dapper**
- **ASP.NET Core** (parameter parsing)
- **Frontend Grids** (AG Grid, Kendo UI adapters)

## Why use it?
s
s
Instead of writing custom parsing logic or separate query classes for each data provider, FlexQuery.NET gives you the **same query syntax** across different providers.

### EF Core Endpoint
```http
GET /api/ef/customers?filter=Status:eq:Active&sort=LastName:asc&page=1&pageSize=20
```

### Dapper Endpoint
```http
GET /api/dapper/customers?filter=Status:eq:Active&sort=LastName:asc&page=1&pageSize=20
```

---

## Project Structure

```text
samples/FlexQuery.NET.Samples.WebApi/
├── Controllers/
│   ├── EfCustomersController.cs       # EF Core querying
│   ├── DapperCustomersController.cs   # Dapper querying
│   ├── AgGridController.cs            # AG Grid Server-Side Row Model adapter
│   └── KendoController.cs             # Kendo UI Grid DataSource adapter
│
├── Data/
│   ├── AppDbContext.cs                # EF Core DbContext (using SQLite)
│   └── SeedData.cs                    # 100 Customers & 500 Orders seeding logic
│
├── Models/
│   ├── Customer.cs                    # Customer entity
│   └── Order.cs                       # Order entity
│
├── wwwroot/
│   ├── aggrid.html                    # Frontend AG Grid demonstration
│   └── kendo.html                     # Frontend Kendo UI Grid demonstration
│
├── Program.cs                         # Startup and Dependency Injection
└── README.md                          # This file
```

---

## Running the Sample

1. **Navigate to the project directory**:
   ```bash
   cd samples/FlexQuery.NET.Samples.WebApi
   ```

2. **Run the application**:
   ```bash
   dotnet run
   ```

3. **Open Swagger UI**:
   Go to [http://localhost:5000/](http://localhost:5000/) in your web browser.

---

## Sample Queries

You can execute these directly in Swagger or in your browser/HTTP client.

### 1. Filtering
Filter operators follow the format `FieldName:operator:Value`. Supported operators include `eq`, `neq`, `contains`, `startswith`, `endswith`, `gt`, `gte`, `lt`, `lte`.

- **Filter Active Customers**:
  ```http
  GET http://localhost:5000/api/ef/customers?filter=Status:eq:Active
  ```
- **Filter Customers in New York**:
  ```http
  GET http://localhost:5000/api/ef/customers?filter=City:eq:New York
  ```
- **Filter Emails Containing "smith"**:
  ```http
  GET http://localhost:5000/api/ef/customers?filter=Email:contains:smith
  ```

### 2. Sorting
Sort fields follow the format `FieldName:asc` or `FieldName:desc`. You can comma-separate multiple sort rules.

- **Sort by Last Name Ascending**:
  ```http
  GET http://localhost:5000/api/ef/customers?sort=LastName:asc
  ```
- **Multi-column Sorting (City asc, CreatedDate desc)**:
  ```http
  GET http://localhost:5000/api/ef/customers?sort=City:asc,CreatedDate:desc
  ```

### 3. Paging
Specify `page` (1-indexed) and `pageSize`.

- **Get page 2 with 10 items**:
  ```http
  GET http://localhost:5000/api/ef/customers?page=2&pageSize=10
  ```

### 4. Projection (Select)
Return only a subset of properties to minimize bandwidth.

- **Only retrieve Id, FirstName, and Email**:
  ```http
  GET http://localhost:5000/api/ef/customers?select=Id,FirstName,Email
  ```

### 5. Eager-Loading (Include)
Include navigation properties.

- **Load customers with their orders**:
  ```http
  GET http://localhost:5000/api/ef/customers?include=Orders
  ```

---

## Frontend Demos

When the API is running, you can access interactive frontend grid demos designed to communicate with the corresponding controllers:

- **AG Grid Server-Side Row Model Demo**:
  [http://localhost:5000/aggrid.html](http://localhost:5000/aggrid.html)
- **Kendo UI Grid Server-Side Demo**:
  [http://localhost:5000/kendo.html](http://localhost:5000/kendo.html)
