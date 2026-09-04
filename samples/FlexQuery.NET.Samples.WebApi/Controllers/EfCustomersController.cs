using System.Diagnostics;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Samples.WebApi.Data;
using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

[ApiController]
[Route("api/ef/customers")]
[Produces("application/json")]
public sealed class EfCustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(QueryResult<Customer>), 200)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var options = parameters.ToQueryOptions();

        var collector = new FlexQueryDiagnosticsCollector();
        var sw = Stopwatch.StartNew();

        var result = await db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters,
                cancellationToken: cancellationToken);
        sw.Stop();

        var report = collector.BuildReport(provider: "EF Core", translator: "Sqlite");

        var diagnostics = DiagnosticsHelper.BuildRichDiagnostics(
            report, options, parameters, result, "/api/ef/customers", sw.Elapsed.TotalMilliseconds);

        return Ok(new
        {
            data = result.Data,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            __diagnostics = diagnostics
        });
    }

    [HttpGet("dto")]
    [ProducesResponseType(typeof(QueryResult<CustomerDto>), 200)]
    public async Task<IActionResult> GetCustomersDto(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await db.Customers
            .AsNoTracking()
            .FlexQueryAsync<Customer, CustomerDto>(parameters, cancellationToken: cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Tracked-query variant: opts out of FlexQuery's default no-tracking execution so
    /// EF populates the inverse navigation (Order.Customer) via relationship fixup.
    /// Demonstrates that QueryResult serialization remains cycle-safe.
    /// </summary>
    [HttpGet("tracked")]
    [ProducesResponseType(typeof(QueryResult<Customer>), 200)]
    public async Task<IActionResult> GetCustomersTracked(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await db.Customers
            .FlexQueryAsync(parameters, opt => opt.UseNoTracking = false,
                cancellationToken: cancellationToken);

        return Ok(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(QueryResult<CustomerSummaryDto>), 200)]
    public async Task<IActionResult> GetCustomersSummary(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await db.Customers
            .AsNoTracking()
            .FlexQueryAsync<Customer, CustomerSummaryDto>(parameters, opt =>
                opt.CreateMap<Customer, CustomerSummaryDto>()
                    .ForMember(dto => dto.CustomerName, entity => entity.FirstName),
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Nested-select DTO endpoint: the root select tree controls the nested response
    /// shape (select=customerName,orders(id,orderDate,status)) while expand controls
    /// which records load (filter/sort/take). Both combine into one server-side query.
    /// </summary>
    [HttpGet("order-summaries")]
    [ProducesResponseType(typeof(QueryResult<CustomerOrderSummaryDto>), 200)]
    public async Task<IActionResult> GetCustomerOrderSummaries(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await db.Customers
            .AsNoTracking()
            .FlexQueryAsync<Customer, CustomerOrderSummaryDto>(parameters, opt =>
                opt.CreateMap<Customer, CustomerOrderSummaryDto>()
                    .ForMember(dto => dto.CustomerName, entity => entity.FirstName)
                    .ForNavigation(dto => dto.Orders, entity => entity.Orders),
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Renamed-navigation DTO endpoint: <c>Purchases</c> maps to the entity's
    /// <c>Orders</c> collection via ForNavigation.
    /// </summary>
    [HttpGet("purchases")]
    [ProducesResponseType(typeof(QueryResult<CustomerWithPurchasesDto>), 200)]
    public async Task<IActionResult> GetCustomersWithPurchases(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await db.Customers
            .AsNoTracking()
            .FlexQueryAsync<Customer, CustomerWithPurchasesDto>(parameters, opt =>
            {
                opt.CreateMap<Customer, CustomerWithPurchasesDto>()
                    .ForMember(dto => dto.CustomerName, entity => entity.FirstName)
                    .ForNavigation(dto => dto.Purchases, entity => entity.Orders);
            },
            cancellationToken: cancellationToken);

        return Ok(result);
    }
}
