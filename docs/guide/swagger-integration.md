# Swagger / OpenAPI Integration

FlexQuery.NET is designed to be "Swagger-friendly" out of the box. Because it uses a standard POCO DTO (`FlexQueryParameters`) with clean XML comments, your API documentation will automatically include detailed information about filtering, sorting, and paging parameters.

## Basic Usage

When you use `FlexQueryParameters` in your controller actions, ASP.NET Core and Swashbuckle automatically detect the properties and surface them as query parameters in the Swagger UI.

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync(parameters, options => 
    {
        options.AllowedFields = ["Id", "Name", "Email"];
    });

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

In your `Program.cs`, configure Swagger to include both your project's XML comments and the FlexQuery library's XML comments.

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
        typeof(FlexQueryParameters).Assembly.GetName().Name + ".xml"
    );

    if (File.Exists(flexXml))
    {
        c.IncludeXmlComments(flexXml);
    }
});
```

## What You Get in Swagger UI

Once configured, the Swagger UI will show a rich interface for your dynamic endpoint:

- **filter**: Shows examples for the DSL format (`field:operator:value`).
- **query**: Explains the FQL (Jira Query Language) syntax.
- **select**: Lists available fields for dynamic projection.
- **page/pageSize**: Clearly documented integer parameters for pagination.
- **Default Values**: Swagger will correctly show the default page size (20) and other settings.

### Schema Documentation
If you drill down into the schema, you can see the internal structure of `FlexQueryParameters`, which is useful for programmatic clients using tools like NSwag or AutoRest to generate client-side SDKs.
