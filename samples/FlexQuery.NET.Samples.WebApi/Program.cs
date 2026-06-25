using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Samples.WebApi.Data;
using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Add SQLite Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=flexquery_sample.db"));

// 2. Configure Dapper Mapping Registry for FlexQuery.NET
var dapperRegistry = new MappingRegistry();
dapperRegistry.Entity<Customer>()
    .ToTable("Customers")
    .HasMany(c => c.Orders).WithForeignKey("CustomerId");

dapperRegistry.Entity<Order>()
    .ToTable("Orders");

builder.Services.AddSingleton<IMappingRegistry>(dapperRegistry);

// 3. Add Controllers and Configure JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// 4. Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FlexQuery.NET Official Sample Web API",
        Version = "v1",
        Description = """
            A unified, lightweight demonstration of **FlexQuery.NET** across multiple ORMs and frontend grid adapters.

            ### Quick Start Sample Queries (EF Core & Dapper)
            
            Every endpoint supports these query parameters:
            
            | Parameter  | Description                      | Example |
            |------------|----------------------------------|---------|
            | `filter`   | Filter expression (DSL)          | `Status:eq:Active` or `City:eq:New York` |
            | `sort`     | Sorting expression               | `LastName:asc` or `CreatedDate:desc` |
            | `page`     | Page number (1-based)            | `1` |
            | `pageSize` | Number of items per page         | `20` |
            | `select`   | Projection: fields to return     | `Id,FirstName,LastName,Email` |
            | `include`  | Relationships to eager-load      | `Orders` |

            ### Interactive Demos
            - **Landing Page**: [http://localhost:5000/](http://localhost:5000/)
            - **AG Grid SSRM Demo**: [http://localhost:5000/aggrid.html](http://localhost:5000/aggrid.html)
            - **Kendo UI Grid Demo**: [http://localhost:5000/kendo.html](http://localhost:5000/kendo.html)
            - **Provider Comparison**: [http://localhost:5000/comparison.html](http://localhost:5000/comparison.html)
            - **FlexQuery Playground**: [http://localhost:5000/playground.html](http://localhost:5000/playground.html)
            """,
        Contact = new OpenApiContact
        {
            Name = "FlexQuery.NET GitHub Repository",
            Url = new Uri("https://github.com/peterjohncasasola/FlexQuery.NET")
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Configure CORS to allow frontend integrations
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
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
