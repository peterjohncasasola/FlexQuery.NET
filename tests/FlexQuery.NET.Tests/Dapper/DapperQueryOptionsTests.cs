using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Dapper;

public class DapperQueryOptionsTests
{
    [Fact]
    public void ToQueryExecutionOptions_PreservesAllBaseOptions()
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
            CaseInsensitiveFields = false,
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

        var converted = options.ToQueryExecutionOptions();

        converted.AllowedFields.Should().BeSameAs(options.AllowedFields);
        converted.BlockedFields.Should().BeSameAs(options.BlockedFields);
        converted.AllowedIncludes.Should().BeSameAs(options.AllowedIncludes);
        converted.ExpressionMappings.Should().BeSameAs(options.ExpressionMappings);
        converted.AllowedOperators.Should().BeSameAs(options.AllowedOperators);
        converted.FilterableFields.Should().BeSameAs(options.FilterableFields);
        converted.SortableFields.Should().BeSameAs(options.SortableFields);
        converted.SelectableFields.Should().BeSameAs(options.SelectableFields);
        converted.MaxFieldDepth.Should().Be(3);
        converted.StrictFieldValidation.Should().BeTrue();
        converted.IncludeTotalCount.Should().BeFalse();
        converted.DefaultPageSize.Should().Be(17);
        converted.MaxPageSize.Should().Be(50);
        converted.CaseInsensitiveFields.Should().BeFalse();
        converted.FieldMappings.Should().BeSameAs(options.FieldMappings);
        converted.FieldAccessResolver.Should().BeSameAs(resolver);
        converted.RoleAllowedFields.Should().BeSameAs(options.RoleAllowedFields);
        converted.CurrentRole.Should().Be("admin");
        converted.AllowedFieldsResolver.Should().BeSameAs(options.AllowedFieldsResolver);
    }

    [Fact]
    public void CopyConstructor_PreservesAllBaseOptions()
    {
        var source = new QueryExecutionOptions
        {
            AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = new(StringComparer.OrdinalIgnoreCase) { "contains" }
            },
            FieldMappings = new(StringComparer.OrdinalIgnoreCase) { ["displayName"] = "Name" },
            CurrentRole = "reader",
            RoleAllowedFields = new(StringComparer.OrdinalIgnoreCase)
            {
                ["reader"] = new(StringComparer.OrdinalIgnoreCase) { "Name" }
            }
        };

        var copied = new DapperQueryOptions(source);

        copied.AllowedOperators.Should().BeSameAs(source.AllowedOperators);
        copied.FieldMappings.Should().BeSameAs(source.FieldMappings);
        copied.CurrentRole.Should().Be("reader");
        copied.RoleAllowedFields.Should().BeSameAs(source.RoleAllowedFields);
    }

    [Fact]
    public void Entity_WhenRegistryIsMissing_PersistsConfiguredMapping()
    {
        var options = new DapperQueryOptions();

        options.Entity<TestEntity>().ToTable("custom_entities");

        options.MappingRegistry.Should().NotBeNull();
        options.MappingRegistry!.GetMapping(typeof(TestEntity)).TableName
            .Should().Be("custom_entities");
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
