> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Security & Field Access

FlexQuery.NET provides an enterprise-grade security pipeline that runs *before* database execution, ensuring your schema remains protected even when exposing dynamic querying capabilities.

## Field-Level Access Control

You can strictly control which fields are accessible to clients. These rules are integrated directly into the validation pipeline.

### AllowedFields (Whitelist)
Only fields in this list can be queried. Any attempt to access a field not in this list results in a validation error.
```csharp
options.AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
{ 
    "Id", "Name", "Orders.*" // Wildcards supported
};
```

### BlockedFields (Blacklist)
Explicitly forbid access to sensitive fields, regardless of any whitelist.
```csharp
options.BlockedFields = new HashSet<string> { "PasswordHash", "SSN" };
```

### Operation-Specific Rules
For more granular control, you can restrict fields by the operation type:
```csharp
options.FilterableFields = new HashSet<string> { "Status", "CreatedAt" };
options.SortableFields = new HashSet<string> { "CreatedAt" };
options.SelectableFields = new HashSet<string> { "Id", "Name", "Status" };
```

### MaxFieldDepth
Prevent "Denial of Service" via deeply nested joins.
```csharp
options.MaxFieldDepth = 3; // Denies 'Orders.Items.Product.Category.Name'
```

## Validation Engine

The `QueryValidator` inspects the `QueryOptions` against the target entity type and your security configuration.

### Validation Result
If validation fails, a `QueryValidationException` is thrown containing a `ValidationResult`.

```json
{
  "isValid": false,
  "errors": [
    {
      "code": "FIELD_NOT_ALLOWED",
      "message": "Field 'PasswordHash' is not allowed",
      "field": "PasswordHash"
    },
    {
      "code": "MAX_DEPTH_EXCEEDED",
      "message": "Field path 'Orders.Items.Product.Category' exceeds max depth of 3",
      "field": "Orders.Items.Product.Category"
    }
  ]
}
```

## Where should I configure AllowedFields?

There are multiple ways to configure field-level security, and choosing the right one depends on your application's size and complexity.

### 1. Controller-Level Configuration (Basic)
You can manually set the security rules within your controller action.

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromQuery] QueryRequest request)
{
    var options = QueryOptionsParser.Parse(request);

    options.AllowedFields = new HashSet<string>
    {
        "Id", "Name", "Email"
    };

    return Ok(await _context.Users.ApplyValidatedQueryOptions(options).ToListAsync());
}
```

- **Use Case**: Quick and simple; useful for small projects or prototypes.
- **Cons**: Not ideal for large systems as it leads to logic duplication across multiple controllers.

### 2. Attribute-Based Configuration (Recommended)
Use the `[FieldAccess]` attribute to declare your security rules directly on your controller classes or methods.

```csharp
[FieldAccess(Allowed = new[] { "Id", "Name", "Email" })]
public class UsersController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] QueryOptions options)
    {
        // Rules are automatically merged into 'options' by the middleware
        return Ok(await _context.Users.ApplyValidatedQueryOptions(options).ToListAsync());
    }
}
```

- **Use Case**: Large-scale APIs where you want declarative and clean code.
- **Pros**: Integrates with the ASP.NET Core pipeline, supports overrides at the action level, and keeps controllers focused on business logic.

### 3. Centralized Resolver (Best for Large Applications)
Create a centralized service to manage security rules for all your entities in one place.

```csharp
public interface IFieldAccessResolver
{
    void Apply<T>(QueryOptions options);
}

public class DefaultFieldAccessResolver : IFieldAccessResolver
{
    public void Apply<T>(QueryOptions options)
    {
        if (typeof(T) == typeof(User))
        {
            options.AllowedFields = new HashSet<string> { "Id", "Name", "Email" };
        }
        else if (typeof(T) == typeof(Order))
        {
            options.AllowedFields = new HashSet<string> { "Id", "Status", "Total" };
        }
    }
}
```

- **Use Case**: Enterprise applications with many entities and complex security requirements.
- **Pros**: Centralizes rules, prevents duplication, and is highly maintainable and scalable.

### 4. Combining Approaches
These approaches are not mutually exclusive. You can use a centralized resolver for global defaults and then apply action-specific overrides via attributes or direct controller configuration.

```csharp
// 1. Apply global defaults
resolver.Apply<User>(options);

// 2. Add action-specific override
options.AllowedFields.Add("Orders.*");
```

### Summary Table

| Approach | Use Case | Pros | Cons |
| :--- | :--- | :--- | :--- |
| **Controller-Level** | Prototypes, Small Apps | Fast setup, Local visibility | Hard to maintain, Duplication |
| **Attribute-Based** | Most REST APIs | Declarative, Clean, Flexible | Tied to ASP.NET Core controllers |
| **Centralized Resolver** | Enterprise, Large Scale | Single source of truth, Scalable | More initial setup required |

### Best Practice Recommendation

- **For Public APIs**: Do **NOT** rely only on controller-level configuration. It is easy to forget a field or duplicate logic incorrectly.
- **Prefer**: Use **Attribute-Based** configuration for most scenarios, or a **Centralized Resolver** if you have complex, entity-wide rules.
- **Enforcement**: Always enforce security (via `ApplyValidatedQueryOptions` or a manual `QueryValidator` check) before executing any queries against your database.

## Injection Protection

FlexQuery.NET implements defense-in-depth against injection attacks:

1.  **Strict Parameterization**: All user-provided values are handled as `Expression.Constant` and translated to parameterized SQL (e.g., `@p0`) by EF Core.
2.  **Syntax Hardening**: The JQL and DSL parsers strictly validate tokens. Malicious inputs like `;`, `--`, or `DROP` cause an immediate `JqlParseException`.
3.  **Alias Regex**: Dynamic projection aliases must match `^[a-zA-Z0-9_]+$`, neutralizing projection-based mapping attacks.

## Best Practices

- **Use QueryRequest**: Bind to the `QueryRequest` DTO instead of `Request.Query` to prevent clients from overriding security rules via the query string.
- **Fail Fast**: Always use `ApplyValidatedQueryOptions` to ensure validation happens before the query is sent to the database.
- **Default to Deny**: In production, always set an `AllowedFields` whitelist rather than relying solely on blacklisting.

