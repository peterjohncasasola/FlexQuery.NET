using FlexQuery.NET.Adapters.Kendo;
using FlexQuery.NET.Adapters.Kendo.Models;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Samples.WebApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

[ApiController]
[Route("api/kendo/customers")]
[Produces("application/json")]
public sealed class KendoController : ControllerBase
{
    private readonly AppDbContext _db;

    public KendoController(AppDbContext db) => _db = db;

    [HttpPost]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetCustomers(
        [FromBody] KendoRequest request,
        CancellationToken cancellationToken)
    {
        var options = request.ToQueryOptions();
        options.IncludeCount = true;

        var collector = new FlexQueryDiagnosticsCollector();

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(options,
                cancellationToken: cancellationToken,
                configureExecution: cfg => cfg.Listener = collector);

        var report = collector.BuildReport(provider: "EF Core", translator: "Sqlite");

        return Ok(new
        {
            data = result.Data,
            total = result.TotalCount,
            __diagnostics = new
            {
                request = new
                {
                    page = request.Page,
                    pageSize = request.PageSize,
                    skip = request.Skip,
                    take = request.Take,
                    filter = request.Filter,
                    sort = request.Sort,
                    filterCount = request.Filter?.Filters?.Count ?? 0,
                    sortCount = request.Sort?.Count ?? 0
                },
                report = DiagnosticsHelper.BuildReportShape(report),
                provider = "EF Core",
                adapter = "Kendo"
            }
        });
    }
}
