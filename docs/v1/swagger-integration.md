> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Swagger / OpenAPI Integration

FlexQuery.NET is designed to be "Swagger-friendly" out of the box. Because it uses a standard DTO (`FlexQueryRequest`) with clean XML comments, your API documentation will automatically include detailed information about filtering, sorting, and paging parameters without any additional library dependencies.

## Basic Usage

When you use `FlexQueryRequest` in your controller actions, ASP.NET Core and Swashbuckle automatically detect the properties and surface them as query parameters in the Swagger UI.

```csharp
[HttpGet]
public IActionResult Get([FromQuery] FlexQueryRequest query)
{
    var result = _context.Entities
        .ApplyFlexQuery(query)
        .ToList();

    return Ok(result);
}
```

## Enable XML Documentation

To see descriptions and examples for `filter`, `sort`, and other parameters in Swagger, you must enable XML documentation in your project file (`.csproj`).

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <!-- Optional: Suppress warnings for missing XML comments on other parts of your code -->
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

## Swagger Configuration

In your `Program.cs` (or `Startup.cs`), configure Swagger to include both your project's XML comments and the FlexQuery library's XML comments.

```csharp
using System.Reflection;
using FlexQuery.NET.Models;

builder.Services.AddSwaggerGen(c =>
{
    // 1. Include your own project's XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // 2. Include FlexQuery.NET XML comments for parameter descriptions
    var flexXml = Path.Combine(
        AppContext.BaseDirectory,
        typeof(FlexQueryRequest).Assembly.GetName().Name + ".xml"
    );

    if (File.Exists(flexXml))
    {
        c.IncludeXmlComments(flexXml);
    }
});
```

## What You Get

Once configured, the following features appear automatically in your Swagger UI:

- **Automatic Parameters**: `filter`, `sort`, `select`, `page`, and `pageSize` appear as standard query fields.
- **Rich Descriptions**: Each field includes a description derived directly from the FlexQuery source code.
- **Built-in Examples**: Swagger UI will show placeholder examples (e.g., `Name:contains:John`) to help developers understand the DSL syntax.

## Example Swagger Query

A developer using your Swagger UI can immediately test queries like:

`?filter=age:gt:25&sort=createdDate:desc`

## Notes

- **Zero Dependencies**: You don't need a `FlexQuery.NET.Swagger` package just to get basic documentation.
- **Universal**: This works with any OpenAPI generator (like Swashbuckle or NSwag) that supports standard .NET XML documentation.
- **DTO-Driven**: The documentation is tied to the `FlexQueryRequest` model, ensuring it stays in sync with the library version you are using.

---

## Future Enhancements (Optional)

In the future, an optional `FlexQuery.NET.Swagger` package may be available to provide even deeper integration, such as:
- Custom Schema Filters for richer operator hints.
- Auto-complete for field names.
- Enhanced validation error responses in the OpenAPI schema.

