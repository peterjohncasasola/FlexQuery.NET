using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Exceptions;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlKeysetBuilderTests
{
    private readonly IMappingRegistry _registry = new MappingRegistry();
    private static readonly ISqlDialect Dialect = new SqlServerDialect();

    public SqlKeysetBuilderTests()
    {
        _registry.Entity<KeysetTestEntity>().ToTable("entities");
    }

    private static QueryOptions Options(Action<QueryOptions> configure)
    {
        var options = new QueryOptions();
        configure(options);
        options.Items[ContextKeys.EntityType] = typeof(KeysetTestEntity);
        return options;
    }

    // ── Keyset mode: first page (cursor null) ────────────────────────────

    [Fact]
    public void Translate_KeysetModeFirstPage_GeneratesOrderByAndLimit()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Id" }];
            o.Paging.PageSize = 10;
            o.IsKeysetMode = true;
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("[Id]");
        command.Sql.Should().NotContain("WHERE");
        command.Sql.Should().NotContain("OFFSET");
        command.Parameters.Should().ContainKey("@PageSize");
        command.Parameters["@PageSize"].Should().Be(10);
    }

    // ── Keyset mode: single column ───────────────────────────────────────

    [Fact]
    public void Translate_KeysetModeSingleAscending_GeneratesSeekPredicate()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Id" }];
            o.Paging.PageSize = 5;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(3);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Id] > @p0");
        command.Sql.Should().Contain("ORDER BY [Id]");
        command.Sql.Should().NotContain("OFFSET");
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters["@p0"].Should().Be(3);
        command.Parameters.Should().ContainKey("@PageSize");
    }

    [Fact]
    public void Translate_KeysetModeSingleDescending_GeneratesSeekPredicate()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Id", Descending = true }];
            o.Paging.PageSize = 5;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(10);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Id] < @p0");
        command.Sql.Should().Contain("ORDER BY [Id] DESC");
        command.Sql.Should().NotContain("OFFSET");
        command.Parameters["@p0"].Should().Be(10);
    }

    // ── Keyset mode: composite key ──────────────────────────────────────

    [Fact]
    public void Translate_KeysetModeCompositeKey_GeneratesOrChain()
    {
        var options = Options(o =>
        {
            o.Sort =
            [
                new SortNode { Field = "City" },
                new SortNode { Field = "Id" }
            ];
            o.Paging.PageSize = 5;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor("New York", 3);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        var sql = command.Sql;
        sql.Should().Contain("([City] > @p0)");
        sql.Should().Contain("([City] = @p0 AND [Id] > @p1)");
        sql.Should().Contain("ORDER BY [City], [Id]");
        sql.Should().NotContain("OFFSET");
        command.Parameters["@p0"].Should().Be("New York");
        command.Parameters["@p1"].Should().Be(3);
    }

    [Fact]
    public void Translate_KeysetModeThreeColumnSort_GeneratesFullOrChain()
    {
        var options = Options(o =>
        {
            o.Sort =
            [
                new SortNode { Field = "Status" },
                new SortNode { Field = "City", Descending = true },
                new SortNode { Field = "Id" }
            ];
            o.Paging.PageSize = 5;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor("Active", "New York", 3);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        var sql = command.Sql;
        sql.Should().Contain("([Status] > @p0)");
        sql.Should().Contain("([Status] = @p0 AND [City] < @p1)");
        sql.Should().Contain("([Status] = @p0 AND [City] = @p1 AND [Id] > @p2)");
        sql.Should().Contain("ORDER BY [Status], [City] DESC, [Id]");
        sql.Should().NotContain("OFFSET");
        command.Parameters["@p0"].Should().Be("Active");
        command.Parameters["@p1"].Should().Be("New York");
    }

    // ── Keyset mode: filter merging ─────────────────────────────────────

    [Fact]
    public void Translate_KeysetModeWithFilter_MergesWhereWithAnd()
    {
        var options = Options(o =>
        {
            o.CaseInsensitive = false;
            o.Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }]
            };
            o.Sort = [new SortNode { Field = "Id" }];
            o.Paging.PageSize = 5;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(3);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        var sql = command.Sql;
        sql.Should().Contain("[Status] = @p0");
        sql.Should().Contain("[Id] > @p1");
        sql.Should().Contain("AND");
        sql.Should().NotContain("OFFSET");
        command.Parameters["@p0"].Should().Be("Active");
        command.Parameters["@p1"].Should().Be(3);
    }

    // ── Keyset mode: null cursor values ─────────────────────────────────

    [Fact]
    public void Translate_KeysetModeWithNullableColumnNullCursor_UsesIsNull()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Name" }];
            o.Paging.PageSize = 5;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(new object?[] { null });
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().NotContain("= @p0");
        command.Sql.Should().Contain("[Name] IS NOT NULL");
        command.Sql.Should().NotContain("OFFSET");
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters["@p0"].Should().BeNull();
    }

    // ── Keyset mode: parameter consistency ──────────────────────────────

    [Fact]
    public void Translate_KeysetMode_ParametersIncludeCursorAndLimit()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Id" }];
            o.Paging.PageSize = 20;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(42);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        command.Parameters.Should().ContainKey("@p0");
        command.Parameters["@p0"].Should().Be(42);
        command.Parameters.Should().ContainKey("@PageSize");
        command.Parameters["@PageSize"].Should().Be(20);
    }

    // ── Keyset mode: disabled paging ────────────────────────────────────

    [Fact]
    public void Translate_KeysetModeNoPaging_DoesNotAddLimit()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Id" }];
            o.Paging.Disabled = true;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(5);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Id] > @p0");
        command.Sql.Should().NotContain("TOP");
        command.Sql.Should().NotContain("LIMIT");
        command.Sql.Should().NotContain("FETCH");
    }

    // ── Non-keyset mode: unchanged offset paging ────────────────────────

    [Fact]
    public void Translate_NonKeysetMode_UsesOffsetPaging()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Id" }];
            o.Paging.Page = 2;
            o.Paging.PageSize = 10;
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("OFFSET");
        command.Sql.Should().Contain("FETCH NEXT");
        command.Parameters.Should().ContainKey("@Offset");
        command.Parameters.Should().ContainKey("@PageSize");
    }

    // ── Keyset mode: error cases ────────────────────────────────────────

    [Fact]
    public void Translate_KeysetModeNoSort_ThrowsInvalidOperationException()
    {
        var options = Options(o =>
        {
            o.Paging.PageSize = 10;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(1);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var act = () => translator.Translate(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one sort field*");
    }

    [Fact]
    public void Translate_KeysetModeCursorCountMismatch_ThrowsQueryValidationException()
    {
        var options = Options(o =>
        {
            o.Sort =
            [
                new SortNode { Field = "City" },
                new SortNode { Field = "Id" }
            ];
            o.Paging.PageSize = 10;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(1);
        });

        var translator = new SqlTranslator(_registry, Dialect);
        var act = () => translator.Translate(options);

        act.Should().Throw<QueryValidationException>()
            .WithMessage("*1 value(s)*2 ordering column*");
    }

    // ── Dialect variants ────────────────────────────────────────────────

    [Fact]
    public void Translate_KeysetModeWithPostgreSql_UsesCorrectQuoting()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Id" }];
            o.Paging.PageSize = 5;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(3);
        });

        var translator = new SqlTranslator(_registry, new PostgreSqlDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("\"Id\" > :p0");
        command.Sql.Should().Contain("ORDER BY \"Id\"");
    }

    [Fact]
    public void Translate_KeysetModeWithMySql_UsesCorrectQuoting()
    {
        var options = Options(o =>
        {
            o.Sort = [new SortNode { Field = "Id" }];
            o.Paging.PageSize = 5;
            o.IsKeysetMode = true;
            o.Cursor = new KeysetCursor(3);
        });

        var translator = new SqlTranslator(_registry, new MySqlDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("`Id` > ?p0");
        command.Sql.Should().Contain("ORDER BY `Id`");
    }

    // ── Test entity ─────────────────────────────────────────────────────

    private sealed class KeysetTestEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string City { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
