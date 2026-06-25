using FlexQuery.NET.Builders;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Samples.WebApi.Data;
using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

[ApiController]
[Route("api/ef/customers")]
[Produces("application/json")]
public sealed class EfCustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public EfCustomersController(AppDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(QueryResult<Customer>), 200)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var options = QueryOptionsParser.Parse(parameters);

        object report;
        object? data;
        int? totalCount;
        int? page;
        int? pageSize;

        if (options.GroupBy is { Count: > 0 })
        {
            var query = _db.Customers.AsNoTracking();
            query = QueryBuilder.ApplyFilter(query, options);

            var groupedQuery = QueryBuilder.ApplySelect(query, options);

            string? generatedSql = null;
            try { generatedSql = groupedQuery.ToQueryString(); }
            catch { }

            var result = await groupedQuery.ToListAsync(cancellationToken);
            data = result;
            totalCount = result.Count;
            page = null;
            pageSize = null;

            report = DiagnosticsHelper.BuildManualShape(
                generatedSql, result.Count, provider: "EF Core");
        }
        else
        {
            var collector = new FlexQueryDiagnosticsCollector();

            var result = await _db.Customers
                .AsNoTracking()
                .FlexQueryAsync(parameters,
                    cancellationToken: cancellationToken,
                    configureExecution: cfg => cfg.Listener = collector);

            data = result.Data;
            totalCount = result.TotalCount;
            page = result.Page;
            pageSize = result.PageSize;

            report = DiagnosticsHelper.BuildReportShape(
                collector.BuildReport(provider: "EF Core", translator: "Sqlite"));
        }

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
