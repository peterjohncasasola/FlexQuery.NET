using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FlexQuery.NET.Models;
using FlexQuery.NET.Serialization;
using Xunit;

namespace FlexQuery.NET.Tests.Serialization;

/// <summary>
/// Regression tests for serialization of query results whose data contains
/// bidirectional object graphs (e.g. Customer.Orders with each Order.Customer
/// back-reference populated by tracked-query fixup or lazy-loading proxies).
/// The QueryResultShapeConverter must serialize with ReferenceHandler.IgnoreCycles
/// so a back-reference writes as null instead of throwing a depth-32 JsonException.
/// </summary>
public class QueryResultCycleSerializationTests
{
    public class CycCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<CycOrder> Orders { get; set; } = new();
    }

    public class CycOrder
    {
        public int Id { get; set; }
        public CycCustomer? Customer { get; set; }
    }

    private static readonly JsonSerializerOptions HostOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new QueryResultShapeConverterFactory() }
    };

    [Fact]
    public void Write_CyclicDataGraph_SerializesBackReferenceAsNull()
    {
        var customer = new CycCustomer { Id = 1, Name = "Melissa" };
        var order = new CycOrder { Id = 93, Customer = customer };
        customer.Orders.Add(order);

        var result = new QueryResult<CycCustomer>
        {
            TotalCount = 1,
            Page = 1,
            PageSize = 20,
            Data = new List<CycCustomer> { customer }
        };

        var act = () => JsonSerializer.Serialize(result, HostOptions);
        act.Should().NotThrow();

        var json = JsonSerializer.Serialize(result, HostOptions);
        var node = JsonNode.Parse(json)!;
        var firstOrder = node["data"]![0]!["orders"]![0]!.AsObject();
        // IgnoreCycles may emit the back-reference as null or omit it entirely —
        // either way the cycle must not throw and scalar fields must survive.
        if (firstOrder.ContainsKey("customer"))
        {
            // JsonNode maps a JSON null value to a null reference.
            firstOrder["customer"].Should().BeNull();
        }
        firstOrder["id"]!.GetValue<int>().Should().Be(93);
    }

    [Fact]
    public void Write_ExplicitPreserveReferenceHandler_IsRespected()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new QueryResultShapeConverterFactory() },
            ReferenceHandler = ReferenceHandler.Preserve
        };

        var customer = new CycCustomer { Id = 1, Name = "Melissa" };
        var order = new CycOrder { Id = 93, Customer = customer };
        customer.Orders.Add(order);

        var result = new QueryResult<CycCustomer>
        {
            Data = new List<CycCustomer> { customer }
        };

        // Explicit user intent: Preserve emits $id/$values metadata, not nulls.
        var json = JsonSerializer.Serialize(result, options);
        json.Should().Contain("$id");
    }

    [Fact]
    public void Write_AcyclicData_UnchangedOutput()
    {
        var result = new QueryResult<CycOrder>
        {
            TotalCount = 1,
            Page = 1,
            PageSize = 20,
            Data = new List<CycOrder> { new() { Id = 93 } }
        };

        var json = JsonSerializer.Serialize(result, HostOptions);
        var node = JsonNode.Parse(json)!;
        node["totalCount"]!.GetValue<int>().Should().Be(1);
        node["data"]![0]!["id"]!.GetValue<int>().Should().Be(93);
    }
}
