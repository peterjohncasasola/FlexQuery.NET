using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Data.Common;
using System.Text.Json.Serialization;
using FlexQuery.NET.AspNetCore.DependencyInjection;

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
[Route("api/diag")]
public class DiagnosticController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok("pong");
}

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IDbConnection _connection;

    public UsersController(IDbConnection connection)
    {
        _connection = connection;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok("Healthy");

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
    {
        try
        {
            var model = BuildModel();
            var result = await ((System.Data.Common.DbConnection)_connection).FlexQueryAsync<SqlCustomer>(parameters, opt =>
            {
                opt.UseModel(model);
            });
            return Ok(result);
        }
        catch (QueryValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static FlexQueryModel BuildModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<SqlCustomer>()
            .ToTable("Customers")
            .HasOne(c => c.Address).WithForeignKey("CustomerId");
        builder.Entity<SqlCustomer>().HasMany(c => c.Orders).WithForeignKey("CustomerId");
        builder.Entity<SqlOrder>()
            .ToTable("Orders")
            .HasMany(o => o.Items).WithForeignKey("OrderId");
        builder.Entity<SqlOrderItem>()
            .ToTable("OrderItems");
        return builder.Build();
    }
}

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IDbConnection _connection;

    public OrdersController(IDbConnection connection)
    {
        _connection = connection;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
    {
        try
        {
            var model = BuildModel();
            var result = await ((System.Data.Common.DbConnection)_connection).FlexQueryAsync<SqlOrder>(parameters, opt =>
            {
                opt.UseModel(model);
            });
            return Ok(result);
        }
        catch (QueryValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static FlexQueryModel BuildModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<SqlCustomer>()
            .ToTable("Customers")
            .HasOne(c => c.Address).WithForeignKey("CustomerId");
        builder.Entity<SqlCustomer>().HasMany(c => c.Orders).WithForeignKey("CustomerId");
        builder.Entity<SqlOrder>()
            .ToTable("Orders")
            .HasMany(o => o.Items).WithForeignKey("OrderId");
        builder.Entity<SqlOrderItem>()
            .ToTable("OrderItems");
        return builder.Build();
    }
}

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IDbConnection _connection;

    public ProductsController(IDbConnection connection)
    {
        _connection = connection;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FlexQueryParameters parameters)
    {
        var model = BuildModel();
        var result = await ((DbConnection)_connection).FlexQueryAsync<SqlOrderItem>(parameters, opt =>
        {
            opt.UseModel(model);
        });
        return Ok(result);
    }

    private static FlexQueryModel BuildModel()
    {
        var builder = new ModelBuilder();
        builder.Entity<SqlCustomer>()
            .ToTable("Customers")
            .HasOne(c => c.Address).WithForeignKey("CustomerId");
        builder.Entity<SqlCustomer>().HasMany(c => c.Orders).WithForeignKey("CustomerId");
        builder.Entity<SqlOrder>()
            .ToTable("Orders")
            .HasMany(o => o.Items).WithForeignKey("OrderId");
        builder.Entity<SqlOrderItem>()
            .ToTable("OrderItems");
        return builder.Build();
    }
}
