# Dapper Conventions

## Overview

The convention system in `FlexQuery.NET.Dapper` automatically discovers table names, column names, foreign keys, and relationships from your C# entity classes — without requiring explicit configuration for every property. It follows a **convention-over-configuration** philosophy: reasonable defaults handle 90% of cases, and explicit overrides handle the rest.

### Why It Exists

Dapper, unlike EF Core, has no built-in model metadata. It doesn't know your table names, column mappings, or foreign key relationships. The convention system bridges this gap by inferring the database schema from your C# types, so you don't need to manually configure every entity.

### When to Use It

- You follow standard naming conventions (PascalCase properties, pluralized table names, `EntityNameId` foreign keys)
- You want zero-configuration entity mapping for simple schemas

### When NOT to Use It

- Your database schema uses unconventional naming — override with the fluent builder API instead
- You need complete control over every mapping detail

## Architecture

```
Entity Type (e.g., Customer)
     │
     ▼
IEntityConvention.Apply()      -> Resolves table name, column mappings
     │
     ▼
IRelationshipConvention.Apply() -> Discovers navigation properties, JOIN metadata
     │      │
     │      ├── IPluralizer           -> "Customer" -> "Customers" (table name)
     │      └── IForeignKeyConvention -> Infers FK column: "CustomerId"
     │
     ▼
EntityMapping                   -> Complete mapping ready for SQL generation
```

## Convention Interfaces

### Fluent Configuration API

Configure entity mappings using `DapperQueryOptions.Entity<TEntity>()`. The fluent builder is the only public API surface — the underlying mapping registry is an internal implementation detail:

```csharp
options.Entity<Customer>()
    .ToTable("Customers")
    .HasKey(c => c.Id)
    .HasMany(c => c.Orders).WithForeignKey("CustomerId");
```

### IEntityConvention

Applied to every entity to configure table name and column mappings:

```csharp
public interface IEntityConvention
{
    void Apply(EntityMapping mapping);
}
```

The `DefaultEntityConvention` maps:
- **Table name** -> Pluralized class name (e.g., `Customer` -> `Customers`)
- **Column names** -> Property names (1:1 by default)
- **Primary key** -> Property named `Id` or `{TypeName}Id`, or decorated with `[Key]`

### IPluralizer

Converts singular entity names to plural table names:

```csharp
public interface IPluralizer
{
    string Pluralize(string name);
}
```

The `DefaultPluralizer` handles common English pluralization rules (e.g., `Customer` -> `Customers`, `Category` -> `Categories`, `Person` -> `People`).

### IForeignKeyConvention

Infers foreign key column names for navigation properties:

```csharp
public interface IForeignKeyConvention
{
    string GetForeignKeyName(
        PropertyInfo navigationProperty,
        Type targetType,
        RelationshipType relationshipType,
        Type entityType);
}
```

The `DefaultForeignKeyConvention` follows the pattern `{NavigationPropertyName}Id` or `{TargetTypeName}Id`.

### IRelationshipConvention

Discovers and configures relationships between entities:

```csharp
internal interface IRelationshipConvention
{
    void Apply(EntityMapping mapping, IMappingRegistry registry);
}
```

The `DefaultRelationshipConvention` inspects navigation properties:
- **Single reference** (e.g., `public Customer Customer { get; set; }`) -> One-to-one or many-to-one
- **Collection** (e.g., `public List<Order> Orders { get; set; }`) -> One-to-many

## Default Convention Behavior

Given this entity:

```csharp
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public List<Order> Orders { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public int CustomerId { get; set; }      // FK inferred
    public Customer Customer { get; set; }   // Navigation property
}
```

The conventions produce:

| Entity | Table Name | Primary Key | FK Column |
|--------|-----------|-------------|-----------|
| `Customer` | `Customers` | `Id` | — |
| `Order` | `Orders` | `Id` | `CustomerId` (references `Customers.Id`) |

## Assembly Scanning

Register all entities from an assembly at startup:

```csharp
opts.ScanEntitiesFromAssembly(typeof(Customer).Assembly);
```

This scans for public, non-abstract classes that have:
- A property named `Id` or `{TypeName}Id`, OR
- A property decorated with `[Key]`, OR
- A `[Table]` attribute on the class

## Fluent Builder Overrides

When conventions don't match your schema, you can use the fluent API to configure entities explicitly.

### Inline Configuration

You can configure entities directly on the options builder:

```csharp
opts.Entity<Customer>()
    .ToTable("tbl_customers")                          // Custom table name
    .Property(c => c.Name, "customer_name")            // Custom column name
    .HasMany<Order>(c => c.Orders, "customer_id");     // Custom FK column
```

### `IEntityTypeConfiguration<TEntity>`

For larger projects, configuring all entities inline becomes messy. Dapper now provides an EF Core-style `IEntityTypeConfiguration<TEntity>` interface to encapsulate mapping logic per entity.

```csharp
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("tbl_customers")
               .HasKey(c => c.Id);
               
        builder.Property(c => c.Name, "customer_name");
        builder.HasMany<Order>(c => c.Orders, "customer_id");
    }
}
```

You can then apply these configurations individually or scan an entire assembly:

```csharp
// Apply individually
opts.ApplyConfiguration(new CustomerConfiguration());

// Or scan an assembly to apply all IEntityTypeConfiguration classes automatically
opts.ApplyConfigurationsFromAssembly(typeof(CustomerConfiguration).Assembly);
```

Explicit configuration always takes precedence over conventions.

## Custom Conventions

Replace the default pluralizer with your own:

```csharp
public class GermanPluralizer : IPluralizer
{
    public string Pluralize(string name) => name + "en";
}
```

Or create a foreign key convention for a different naming pattern:

```csharp
public class SnakeCaseFkConvention : IForeignKeyConvention
{
    public string GetForeignKeyName(
        PropertyInfo navProp, Type targetType,
        RelationshipType relType, Type entityType)
    {
        return $"{ToSnakeCase(targetType.Name)}_id";
    }
}
```

## Best Practices

1. **Follow standard naming** — PascalCase properties and pluralized table names work out of the box
2. **Use `[Table]` and `[Key]` attributes** for non-standard names before resorting to fluent configuration
3. **Call `ScanEntitiesFromAssembly`** at startup to eagerly initialize all mappings and catch configuration errors early
4. **Override only what differs** — Let conventions handle the majority of mappings and use fluent API for exceptions
5. **Test mapping resolution** — Verify that `registry.GetMapping<T>().TableName` returns what you expect

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Table name is singular but DB expects plural | The default pluralizer handles most English words. For edge cases, use `.ToTable()` |
| FK column doesn't follow `{Type}Id` pattern | Use `.HasMany<T>(nav, "fk_column")` or `.HasOne<T>(nav, "fk_column")` |
| Assembly scan misses entities without `Id` property | Add `[Key]` attribute to your primary key property |
| Navigation property not discovered | Ensure the property type is a class (for references) or `ICollection<T>` / `List<T>` (for collections) |

## Related Features

- [Getting Started](/providers/dapper/getting-started) — Installation and first mapping
- [SQL Generation](/providers/dapper/sql-generation) — How mappings are used during translation
- [Relationship Queries](/providers/dapper/relationship-queries) — How relationships generate JOINs
