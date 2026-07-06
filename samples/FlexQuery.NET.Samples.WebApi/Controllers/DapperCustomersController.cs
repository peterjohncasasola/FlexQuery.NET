using System.Diagnostics;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;
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
public sealed class DapperCustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(QueryResult<Customer>), 200)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var options = parameters.ToQueryOptions();
        var connection = db.Database.GetDbConnection();

        var collector = new FlexQueryDiagnosticsCollector();
        var sw = Stopwatch.StartNew();

        var result = await connection.FlexQueryAsync<Customer>(parameters,
            configureDapper: opt =>
            {
                opt.Dialect = new SqliteDialect();
                opt.Entity<Customer>()
                    .ToTable("Customers")
                    .HasMany(c => c.Orders).WithForeignKey("CustomerId");
                opt.Entity<Order>()
                    .ToTable("Orders");
            },
            cancellationToken: cancellationToken,
            configureExecution: cfg => cfg.Listener = collector);

        sw.Stop();

        var report = collector.BuildReport(provider: "Dapper", translator: "Sqlite");

        var diagnostics = DiagnosticsHelper.BuildRichDiagnostics(
            report, options, parameters, result, "/api/dapper/customers", sw.Elapsed.TotalMilliseconds);

        return Ok(new
        {
            data = result.Data,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            __diagnostics = diagnostics
        });
    }
}
