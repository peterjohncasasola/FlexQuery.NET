> [!WARNING]
> **?? This is legacy documentation for FlexQuery.NET v1.x.**
> For the latest version, please see the [v2 Getting Started Guide](/guide/getting-started).


# Debugging Tools

FlexQuery.NET provides a dedicated debug mode to help you understand how your string-based queries are transformed into executable code.

## ToFlexQueryDebug()

The `ToFlexQueryDebug` extension method allows you to inspect the internal state of a query before it is executed.

```csharp
var options = QueryOptionsParser.Parse(Request.Query);
var debug = _context.Customers.ToFlexQueryDebug(options);

// Inspect results
Console.WriteLine(debug.LinqLambda);      // The C#-like LINQ syntax
Console.WriteLine(debug.ExpressionTree);  // The structural expression node tree
Console.WriteLine(debug.Ast);             // The raw parsed Abstract Syntax Tree
```

## Example Analysis

For a query like `?query=orders.any(status = Cancelled AND total > 500)`:

### 1. LINQ Lambda
Shows you exactly what the generated C# code looks like. This is the most helpful for verifying logic.
```csharp
query.Where(x => x.Orders.Any(sc => (sc.Status == "Cancelled") && (sc.Total > 500)))
```

### 2. AST (Abstract Syntax Tree)
Shows how the parser interpreted the input string before it was converted to an expression tree.
```text
orders.any(AND(status eq [Cancelled], total gt [500]))
```

### 3. Expression Tree
A detailed breakdown of the `System.Linq.Expressions.Expression` tree, useful for deep debugging of custom operator handlers or provider-specific translation issues.

## Debugging Tips

- **Check Types**: If a filter isn't working as expected, use debug mode to ensure the value was parsed as the correct type (e.g., ensuring a string "123" was converted to a numeric constant).
- **Verify Logic**: Nesting parentheses in DSL can be tricky. Use the `LinqLambda` output to ensure the logic groups are prioritized correctly.
- **Log in Development**: Consider logging the `LinqLambda` for all dynamic queries during development to build confidence in the system.

