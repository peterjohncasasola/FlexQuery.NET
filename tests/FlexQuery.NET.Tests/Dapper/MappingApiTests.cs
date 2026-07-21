using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Builders;
using FlexQuery.NET.Dapper.Mapping.Metadata;
using FlexQuery.NET.Dapper.Sql.Builders;

namespace FlexQuery.NET.Tests.Dapper;

public class MappingApiTests
{
    [Fact]
    public void EntityTypeBuilder_ToTable_WithSchema_SetsSchema()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>().ToTable("Customers", "dbo");
        var mapping = registry.GetMapping(typeof(Customer));

        mapping.TableName.Should().Be("Customers");
        mapping.Schema.Should().Be("dbo");
    }

    [Fact]
    public void EntityTypeBuilder_Ignore_ExcludesPropertyFromMapping()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>().Ignore(c => c.SSN);

        var mapping = registry.GetMapping(typeof(Customer));

        mapping.GetProperties().Should().NotContain("SSN");
        mapping.IsIgnored("SSN").Should().BeTrue();
    }

    [Fact]
    public void PropertyBuilder_HasColumnName_SetsColumnName()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>().Property(c => c.Name).HasColumnName("full_name");

        var mapping = registry.GetMapping(typeof(Customer));
        mapping.GetColumnName("Name").Should().Be("full_name");
    }

    [Fact]
    public void RelationshipBuilder_HasForeignKey_String_SetsForeignKey()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .HasMany(c => c.Orders)
            .HasForeignKey("CustomerId");

        var mapping = registry.GetMapping(typeof(Customer));
        var rel = mapping.GetRelationship("Orders");

        rel.Should().NotBeNull();
        rel!.ForeignKey.Should().Be("CustomerId");
    }

    [Fact]
    public void RelationshipBuilder_HasForeignKey_Lambda_ExtractsPropertyName()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .HasMany(c => c.Orders)
            .HasForeignKey<Order>(o => o.CustomerId);

        var mapping = registry.GetMapping(typeof(Customer));
        var rel = mapping.GetRelationship("Orders");

        rel.Should().NotBeNull();
        rel!.ForeignKey.Should().Be("CustomerId");
    }

    [Fact]
    public void RelationshipBuilder_HasPrincipalKey_String_SetsPrincipalKey()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .HasOne(c => c.Address)
            .HasPrincipalKey("Id");

        var mapping = registry.GetMapping(typeof(Customer));
        var rel = mapping.GetRelationship("Address");

        rel.Should().NotBeNull();
        rel!.PrincipalKey.Should().Be("Id");
    }

    [Fact]
    public void RelationshipBuilder_HasPrincipalKey_Lambda_ExtractsPropertyName()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .HasOne(c => c.Address)
            .HasPrincipalKey<Address>(a => a.Id);

        var mapping = registry.GetMapping(typeof(Customer));
        var rel = mapping.GetRelationship("Address");

        rel.Should().NotBeNull();
        rel!.PrincipalKey.Should().Be("Id");
    }

    [Fact]
    public void RelationshipType_IsNotPubliclyAccessible()
    {
        var relationshipType = typeof(RelationshipType);
        relationshipType.IsPublic.Should().BeFalse();
    }

    [Fact]
    public void EntityMapping_GetKeyProperties_ReturnsConfiguredKeys()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .HasKey(c => c.Id);

        var mapping = registry.GetMapping(typeof(Customer));
        mapping.GetKeyProperties().Should().Contain("Id");
    }

    [Fact]
    public void HasAlias_IsNotAvailableOnEntityTypeBuilder()
    {
        var builder = typeof(EntityTypeBuilder<Customer>);
        builder.GetMethod("HasAlias").Should().BeNull();
    }

    [Fact]
    public void IsPrimaryKey_IsNotAvailableOnPropertyBuilder()
    {
        var builder = typeof(PropertyBuilder);
        builder.GetMethod("IsPrimaryKey").Should().BeNull();
    }

    [Fact]
    public void RelationshipResolver_ResolvesConfiguredPrimaryKey_WhenNoExplicitPrincipalKey()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .HasKey(c => c.Id)
            .HasMany(c => c.Orders)
            .HasForeignKey("CustomerId");

        var mapping = registry.GetMapping(typeof(Customer));
        var rel = mapping.GetRelationship("Orders");

        var principalColumn = RelationshipResolver.ResolvePrincipalColumn(mapping, rel!);

        principalColumn.Should().Be("Id");
    }

    [Fact]
    public void RelationshipResolver_UsesExplicitPrincipalKey_WhenProvided()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .HasKey(c => c.Id)
            .HasOne(c => c.Address)
            .HasPrincipalKey("Id");

        var mapping = registry.GetMapping(typeof(Customer));
        var rel = mapping.GetRelationship("Address");

        var principalColumn = RelationshipResolver.ResolvePrincipalColumn(mapping, rel!);

        principalColumn.Should().Be("Id");
    }

    [Fact]
    public void RelationshipResolver_FallsBackToId_WhenNoHasKeyConfigured()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .HasMany(c => c.Orders)
            .HasForeignKey("CustomerId");

        var mapping = registry.GetMapping(typeof(Customer));
        var rel = mapping.GetRelationship("Orders");

        var principalColumn = RelationshipResolver.ResolvePrincipalColumn(mapping, rel!);

        principalColumn.Should().Be("Id");
    }

    [Fact]
    public void RelationshipResolver_ThrowsOnCompositeKey_WithoutExplicitPrincipalKey()
    {
        var registry = new MappingRegistry();
        registry.Entity<Order>()
            .HasKey(o => new { o.Id, o.CustomerId })
            .HasMany(o => o.OrderItems)
            .HasForeignKey("OrderId");

        var mapping = registry.GetMapping(typeof(Order));
        var rel = mapping.GetRelationship("OrderItems");

        Action act = () => RelationshipResolver.ResolvePrincipalColumn(mapping, rel!);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*composite primary keys*")
            .WithMessage("*HasPrincipalKey*");
    }

    [Fact]
    public void SqlJoinCondition_UsesConfiguredPrimaryKey_NotIdConvention()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>()
            .ToTable("Customers")
            .HasKey(c => c.Id)
            .HasMany(c => c.Orders)
            .HasForeignKey("CustomerId");

        registry.Entity<Order>()
            .ToTable("Orders")
            .HasKey(o => o.Id);

        var mapping = registry.GetMapping(typeof(Customer));
        var rel = mapping.GetRelationship("Orders");
        var orderMapping = registry.GetMapping(typeof(Order));
        var dialect = new SqliteDialect();

        var joinCondition = SqlSyntaxBuilder.BuildJoinCondition(
            dialect,
            rel!,
            mapping,
            mapping.TableName,
            orderMapping,
            "Orders");

        joinCondition.Should().Contain($"{dialect.QuoteIdentifier("Customers")}.{dialect.QuoteIdentifier("Id")}");
    }

    private sealed class CustomerWithCustomPk
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderWithCustomPk> Orders { get; set; } = [];
    }

    private sealed class OrderWithCustomPk
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public CustomerWithCustomPk? Customer { get; set; }
    }

    [Fact]
    public void RelationshipResolver_UsesCustomKeyColumn_WhenHasKeyConfigured()
    {
        var registry = new MappingRegistry();
        registry.Entity<CustomerWithCustomPk>()
            .HasKey(c => c.CustomerId)
            .HasMany(c => c.Orders)
            .HasForeignKey("CustomerId");

        var mapping = registry.GetMapping(typeof(CustomerWithCustomPk));
        var rel = mapping.GetRelationship("Orders");

        var principalColumn = RelationshipResolver.ResolvePrincipalColumn(mapping, rel!);

        principalColumn.Should().Be("CustomerId");
    }

    [Fact]
    public void SqlJoinCondition_UsesCustomKeyColumn_WhenHasKeyConfigured()
    {
        var registry = new MappingRegistry();
        registry.Entity<CustomerWithCustomPk>()
            .ToTable("Customers")
            .HasKey(c => c.CustomerId)
            .HasMany(c => c.Orders)
            .HasForeignKey("CustomerId");

        registry.Entity<OrderWithCustomPk>()
            .ToTable("Orders")
            .HasKey(o => o.OrderId);

        var mapping = registry.GetMapping(typeof(CustomerWithCustomPk));
        var rel = mapping.GetRelationship("Orders");
        var orderMapping = registry.GetMapping(typeof(OrderWithCustomPk));
        var dialect = new SqliteDialect();

        var joinCondition = SqlSyntaxBuilder.BuildJoinCondition(
            dialect,
            rel!,
            mapping,
            mapping.TableName,
            orderMapping,
            "Orders");

        joinCondition.Should().Contain($"{dialect.QuoteIdentifier("Customers")}.{dialect.QuoteIdentifier("CustomerId")}");
        joinCondition.Should().NotContain($"{dialect.QuoteIdentifier("Customers")}.{dialect.QuoteIdentifier("Id")}");
    }
}
