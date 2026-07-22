using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlSimpleIncludeQueryBuilderTests
{
    [Fact]
    public void Build_PaginatesRootQuery_BeforeJoiningCollection()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;
        var mapping = registry.GetMapping(typeof(Customer));
        var dialect = new SqliteDialect();
        var translator = new SqlTranslator(registry, dialect);
        var options = new QueryOptions
        {
            Includes = ["Orders"],
            Sort = [new SortNode { Field = "Id" }],
            Paging = { Page = 2, PageSize = 10 },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        SqlSimpleIncludeQueryBuilder.CanBuild(options, mapping, registry).Should().BeTrue();

        var command = SqlSimpleIncludeQueryBuilder.Build(options, mapping, registry, dialect, translator);

        command.Sql.Should().Contain("FROM (SELECT");
        command.Sql.Should().Contain("LIMIT @PageSize OFFSET @Offset");
        command.Sql.Should().Contain(") AS \"__fq_root\" LEFT JOIN \"Orders\" AS \"Orders\"");
        command.Sql.IndexOf("LIMIT @PageSize OFFSET @Offset", StringComparison.Ordinal)
            .Should().BeLessThan(command.Sql.IndexOf("LEFT JOIN", StringComparison.Ordinal));
        command.Parameters.Should().ContainKey("@Offset").WhoseValue.Should().Be(10);
        command.Parameters.Should().ContainKey("@PageSize").WhoseValue.Should().Be(10);
    }

    [Fact]
    public void CanBuild_RejectsComplexExpand()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;
        var mapping = registry.GetMapping(typeof(Customer));
        var options = new QueryOptions
        {
            Expand =
            [
                new FlexQuery.NET.Models.Projection.IncludeNode
                {
                    Path = "Orders",
                    Take = 3
                }
            ],
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        SqlSimpleIncludeQueryBuilder.CanBuild(options, mapping, registry).Should().BeFalse();
    }
}
