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

        var result = await db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters,
                cancellationToken: cancellationToken,
                configureExecution: cfg => cfg.Listener = collector);

        var data = result.Data;
        var totalCount = result.TotalCount;
        var page = result.Page;
        var pageSize = result.PageSize;

        var report = DiagnosticsHelper.BuildReportShape(
            collector.BuildReport(provider: "EF Core", translator: "Sqlite"));

        return Ok(new
        {
            data,
            totalCount,
            page,
            pageSize,
            __diagnostics = new
            {
                provider = "EF Core",
                adapter = "FlexQuery",
                report,
                parsedOptions = new
                {
                    filter = options.Filter,
                    sort = options.Sort,
                    select = parameters.Select,
                    include = parameters.Include,
                    groupBy = options.GroupBy,
                    aggregates = options.Aggregates,
                    having = options.Having,
                    paging = options.Paging,
                    filterCount = DiagnosticsHelper.CountFilters(options.Filter),
                    sortCount = options.Sort?.Count ?? 0
                },
                endpoint = "/api/ef/customers"
            }
        });
    }
}
