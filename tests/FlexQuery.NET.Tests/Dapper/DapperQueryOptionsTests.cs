using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Tests.Dapper;

public class DapperQueryOptionsTests
{
    [Fact]
    public void Properties_AreAccessibleViaInheritance()
    {
        var resolver = new AllowAllResolver();
        var options = new DapperQueryOptions
        {
            AllowedFields = new(StringComparer.OrdinalIgnoreCase) { "Id" },
            BlockedFields = new(StringComparer.OrdinalIgnoreCase) { "Secret" },
            AllowedIncludes = new(StringComparer.OrdinalIgnoreCase) { "Orders" },
            AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = new(StringComparer.OrdinalIgnoreCase) { "eq" }
            },
            FilterableFields = new(StringComparer.OrdinalIgnoreCase) { "Name" },
            SortableFields = new(StringComparer.OrdinalIgnoreCase) { "Id" },
            SelectableFields = new(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            MaxFieldDepth = 3,
            StrictFieldValidation = true,
            IncludeTotalCount = false,
            DefaultPageSize = 17,
            MaxPageSize = 50,
            CaseInsensitive = false,
            FieldMappings = new(StringComparer.OrdinalIgnoreCase) { ["displayName"] = "Name" },
            FieldAccessResolver = resolver,
            RoleAllowedFields = new(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new(StringComparer.OrdinalIgnoreCase) { "*" }
            },
            CurrentRole = "admin",
            AllowedFieldsResolver = _ => ["Id"]
        };
        options.MapField<TestEntity, string>("displayName", x => x.Name);

        options.AllowedFields.Should().BeEquivalentTo(new[] { "Id" });
        options.BlockedFields.Should().BeEquivalentTo(new[] { "Secret" });
        options.AllowedIncludes.Should().BeEquivalentTo(new[] { "Orders" });
        options.FilterableFields.Should().BeEquivalentTo(new[] { "Name" });
        options.SortableFields.Should().BeEquivalentTo(new[] { "Id" });
        options.SelectableFields.Should().BeEquivalentTo(new[] { "Id", "Name" });
        options.MaxFieldDepth.Should().Be(3);
        options.StrictFieldValidation.Should().BeTrue();
        options.IncludeTotalCount.Should().BeFalse();
        options.DefaultPageSize.Should().Be(17);
        options.MaxPageSize.Should().Be(50);
        options.CaseInsensitive.Should().BeFalse();
        options.FieldMappings.Should().ContainKey("displayName");
        options.FieldAccessResolver.Should().BeSameAs(resolver);
        options.RoleAllowedFields.Should().ContainKey("admin");
        options.CurrentRole.Should().Be("admin");
        options.AllowedFieldsResolver.Should().NotBeNull();
    }

    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var options = new DapperQueryOptions();
        options.IncludeTotalCount.Should().BeTrue();
        options.CommandTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void UseModel_WhenSet_AppliesConfiguredMappings()
    {
        var builder = new ModelBuilder();
        builder.Entity<TestEntity>()
            .ToTable("custom_entities")
            .HasKey(e => e.Id);
        var model = builder.Build();

        var options = new DapperQueryOptions();
        options.UseModel(model);

        options.Model.Should().NotBeNull();
        options.Model.Should().BeSameAs(model);
    }

    [Fact]
    public void UseModel_WhenNull_ThrowsArgumentNullException()
    {
        var options = new DapperQueryOptions();

        var act = () => options.UseModel(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseModel_ConventionsOnly_WhenNotSet_ReturnsNull()
    {
        var options = new DapperQueryOptions();

        options.Model.Should().BeNull();
    }

    private sealed class AllowAllResolver : IFieldAccessResolver
    {
        public bool IsAllowed(string field, QueryOperation operation, QueryContext context) => true;
    }

    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
