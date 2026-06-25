using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Samples.WebApi.Data;
using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

[ApiController]
[Route("api/dapper/customers")]
[Produces("application/json")]
public sealed class DapperCustomersController(AppDbContext db, IMappingRegistry registry) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(QueryResult<Customer>), 200)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var options = QueryOptionsParser.Parse(parameters);
        var connection = db.Database.GetDbConnection();

        var collector = new FlexQueryDiagnosticsCollector();

        var result = await connection.FlexQueryAsync<Customer>(parameters,
            configureDapper: opt =>
            {
                opt.Dialect = new SqliteDialect();
                opt.MappingRegistry = registry;
                opt.EntityType = typeof(Customer);
            },
            configureExecution: cfg => cfg.Listener = collector);

        var data = result.Data;
        var totalCount = result.TotalCount;
        var page = result.Page;
        var pageSize = result.PageSize;

        var report = DiagnosticsHelper.BuildReportShape(
            collector.BuildReport(provider: "Dapper", translator: "Sqlite"));

        return Ok(new
        {
            data,
            totalCount,
            page,
            pageSize,
            __diagnostics = new
            {
                provider = "Dapper",
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
                endpoint = "/api/dapper/customers"
            }
        });
    }
}
