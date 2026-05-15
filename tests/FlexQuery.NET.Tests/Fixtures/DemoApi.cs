using FlexQuery.NET.AspNetCore.Extensions;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;
using FlexQuery.NET.Dapper.Mapping;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

using System.Text.Json.Serialization;
using FlexQuery.NET.Tests.Models;

namespace FlexQuery.NET.Tests.Fixtures;

public class DemoApiStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(DemoApiStartup).Assembly)
            .AddJsonOptions(options => 
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            })
            .AddFlexQuerySecurity();
        
        var registry = new MappingRegistry();
        
        var customerMapping = new EntityMapping(typeof(SqlCustomer), "Customers");
        customerMapping.MapProperty("Id", "Id");
        customerMapping.MapProperty("Name", "Name");
        customerMapping.MapProperty("Email", "Email");
        customerMapping.MapJoin("Address", typeof(SqlAddress), "Addresses", "\"Customers\".\"Id\" = \"Address\".\"CustomerId\"");
        customerMapping.MapJoin("Orders", typeof(SqlOrder), "Orders", "\"Customers\".\"Id\" = \"Orders\".\"CustomerId\"");
        registry.Register(customerMapping);

        var orderMapping = new EntityMapping(typeof(SqlOrder), "Orders");
        orderMapping.MapProperty("Id", "Id");
        orderMapping.MapProperty("CustomerId", "CustomerId");
        orderMapping.MapProperty("OrderDate", "OrderDate");
        orderMapping.MapProperty("Total", "Total");
        orderMapping.MapJoin("Items", typeof(SqlOrderItem), "OrderItems", "\"Orders\".\"Id\" = \"OrderItems\".\"OrderId\"");
        registry.Register(orderMapping);
        
        var orderItemMapping = new EntityMapping(typeof(SqlOrderItem), "OrderItems");
        orderItemMapping.MapProperty("Id", "Id");
        orderItemMapping.MapProperty("OrderId", "OrderId");
        orderItemMapping.MapProperty("ProductName", "ProductName");
        orderItemMapping.MapProperty("Quantity", "Quantity");
        orderItemMapping.MapProperty("UnitPrice", "UnitPrice");
        registry.Register(orderItemMapping);

        services.AddSingleton<IMappingRegistry>(registry);
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly IMappingRegistry _registry;

    public UsersController(IDbConnection connection, ISqlDialect dialect, IMappingRegistry registry)
    {
        _connection = connection;
        _dialect = dialect;
        _registry = registry;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok("Healthy");

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
    {
        try
        {
            var result = await ((System.Data.Common.DbConnection)_connection).FlexQueryAsync<object>(parameters, opt => 
            {
                opt.Dialect = _dialect;
                opt.MappingRegistry = _registry;
                opt.EntityType = typeof(SqlCustomer);
            });
            return Ok(result);
        }
        catch (FlexQuery.NET.Exceptions.QueryValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly IMappingRegistry _registry;

    public OrdersController(IDbConnection connection, ISqlDialect dialect, IMappingRegistry registry)
    {
        _connection = connection;
        _dialect = dialect;
        _registry = registry;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
    {
        try
        {
            var result = await ((System.Data.Common.DbConnection)_connection).FlexQueryAsync<object>(parameters, opt => 
            {
                opt.Dialect = _dialect;
                opt.MappingRegistry = _registry;
                opt.EntityType = typeof(SqlOrder);
            });
            return Ok(result);
        }
        catch (FlexQuery.NET.Exceptions.QueryValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly IMappingRegistry _registry;

    public ProductsController(IDbConnection connection, ISqlDialect dialect, IMappingRegistry registry)
    {
        _connection = connection;
        _dialect = dialect;
        _registry = registry;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
    {
        // Using SqlOrderItem as "Product" for demo
        var result = await ((DbConnection)_connection).FlexQueryAsync<SqlOrderItem>(parameters, opt => 
        {
            opt.Dialect = _dialect;
            opt.MappingRegistry = _registry;
        });
        return Ok(result);
    }
}
