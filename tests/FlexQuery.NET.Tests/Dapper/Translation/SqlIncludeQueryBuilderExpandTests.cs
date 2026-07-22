using Dapper;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using Microsoft.Data.Sqlite;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlIncludeQueryBuilderExpandTests
{
    [Fact]
    public void BuildIncludeSql_WithExpandTake_GeneratesPartitionedTopNPerParent()
    {
        var registry = CreateRegistry();
        var rootMapping = registry.GetMapping(typeof(ExpandCustomer));
        var childMapping = registry.GetMapping(typeof(ExpandOrder));
        var parameters = new SqlParameterContext(new SqliteDialect());

        var sql = SqlIncludeQueryBuilder.BuildIncludeSql(
            "Orders",
            rootMapping,
            childMapping,
            new SqliteDialect(),
            CreateDeliveredTopThreeExpand(),
            [1, 2],
            parameters);

        sql.Should().Contain("ROW_NUMBER() OVER");
        sql.Should().Contain("PARTITION BY \"Orders\".\"CustomerId\"");
        sql.Should().Contain("ORDER BY \"Orders\".\"OrderDate\" DESC");
        sql.Should().Contain("\"__fq_row_number\" <= @p");
        sql.Should().NotContain("LIMIT");
    }

    [Fact]
    public async Task BuildIncludeSql_WithExpandTake_ReturnsOnlyMatchingTopRowsPerParent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await SeedAsync(connection);

        var registry = CreateRegistry();
        var rootMapping = registry.GetMapping(typeof(ExpandCustomer));
        var childMapping = registry.GetMapping(typeof(ExpandOrder));
        var parameters = new SqlParameterContext(new SqliteDialect());

        var sql = SqlIncludeQueryBuilder.BuildIncludeSql(
            "Orders",
            rootMapping,
            childMapping,
            new SqliteDialect(),
            CreateDeliveredTopThreeExpand(),
            [1, 2],
            parameters);

        var rows = (await connection.QueryAsync(sql, parameters.RawParameters)).ToList();

        rows.Should().HaveCount(6);
        var grouped = rows
            .Select(row => (IDictionary<string, object>)row)
            .GroupBy(row => Convert.ToInt32(row["Orders_CustomerId"]))
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => Convert.ToInt32(row["Orders_Id"])).ToList());

        grouped[1].Should().Equal(105, 104, 103);
        grouped[2].Should().Equal(205, 204, 203);
    }

    private static MappingRegistry CreateRegistry()
    {
        var registry = new MappingRegistry();
        registry.Entity<ExpandCustomer>()
            .ToTable("Customers")
            .HasKey(c => c.Id)
            .HasMany(c => c.Orders)
            .HasForeignKey("CustomerId");

        registry.Entity<ExpandOrder>()
            .ToTable("Orders")
            .HasKey(o => o.Id);

        return registry;
    }

    private static IncludeNode CreateDeliveredTopThreeExpand()
        => new()
        {
            Path = "Orders",
            Take = 3,
            Sort = [new SortNode { Field = "OrderDate", Descending = true }],
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Status",
                        Operator = "eq",
                        Value = "Delivered"
                    }
                ]
            }
        };

    private static async Task SeedAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Customers (Id INTEGER NOT NULL, Name TEXT NOT NULL);
            CREATE TABLE Orders (Id INTEGER NOT NULL, CustomerId INTEGER NOT NULL, OrderDate TEXT NOT NULL, Status TEXT NOT NULL);
            INSERT INTO Customers (Id, Name) VALUES (1, 'Ada'), (2, 'Grace');

            INSERT INTO Orders (Id, CustomerId, OrderDate, Status) VALUES
                (101, 1, '2024-01-01', 'Delivered'),
                (102, 1, '2024-01-02', 'Delivered'),
                (103, 1, '2024-01-03', 'Delivered'),
                (104, 1, '2024-01-04', 'Delivered'),
                (105, 1, '2024-01-05', 'Delivered'),
                (106, 1, '2024-01-06', 'Pending'),
                (201, 2, '2024-02-01', 'Delivered'),
                (202, 2, '2024-02-02', 'Delivered'),
                (203, 2, '2024-02-03', 'Delivered'),
                (204, 2, '2024-02-04', 'Delivered'),
                (205, 2, '2024-02-05', 'Delivered'),
                (206, 2, '2024-02-06', 'Pending');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class ExpandCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ExpandOrder> Orders { get; set; } = [];
    }

    private sealed class ExpandOrder
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
