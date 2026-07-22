using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Materialization;
using FlexQuery.NET.Dapper.Sql.Builders;
using Microsoft.Data.Sqlite;

namespace FlexQuery.NET.Tests.Dapper.Materialization;

public class SimpleIncludeStreamingMaterializerTests
{
    [Fact]
    public async Task MaterializeAsync_AllowsReadingColumnsOutOfOrdinalOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = """
                CREATE TABLE Customers (CustomerId INTEGER NOT NULL, Name TEXT NOT NULL);
                CREATE TABLE Orders (OrderId INTEGER NOT NULL, CustomerId INTEGER NOT NULL, Number TEXT NOT NULL);
                INSERT INTO Customers (CustomerId, Name) VALUES (1, 'Ada');
                INSERT INTO Orders (OrderId, CustomerId, Number) VALUES (10, 1, 'A-10');
                """;
            await setup.ExecuteNonQueryAsync();
        }

        var registry = new MappingRegistry();
        registry.Entity<CustomerWithLateKey>()
            .ToTable("Customers")
            .HasKey(c => c.CustomerId)
            .HasMany(c => c.Orders)
            .HasForeignKey("CustomerId");

        registry.Entity<OrderWithCustomPk>()
            .ToTable("Orders")
            .HasKey(o => o.OrderId);

        var rootMapping = registry.GetMapping(typeof(CustomerWithLateKey));
        var childMapping = registry.GetMapping(typeof(OrderWithCustomPk));

        var result = await SimpleIncludeStreamingMaterializer.MaterializeAsync<CustomerWithLateKey>(
            connection,
            new SimpleIncludeSqlCommand
            {
                Sql = """
                    SELECT
                        c.Name AS Name,
                        c.CustomerId AS CustomerId,
                        o.OrderId AS Orders_OrderId,
                        o.CustomerId AS Orders_CustomerId,
                        o.Number AS Orders_Number
                    FROM Customers c
                    LEFT JOIN Orders o ON o.CustomerId = c.CustomerId
                    """,
                IncludePath = "Orders",
                ChildMapping = childMapping
            },
            rootMapping,
            commandTimeout: null,
            CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].Name.Should().Be("Ada");
        result.Items[0].CustomerId.Should().Be(1);
        result.Items[0].Orders.Should().ContainSingle();
        result.Items[0].Orders[0].OrderId.Should().Be(10);
    }

    private sealed class CustomerWithLateKey
    {
        public string Name { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public List<OrderWithCustomPk> Orders { get; set; } = [];
    }

    private sealed class OrderWithCustomPk
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public string Number { get; set; } = string.Empty;
    }
}
