using FlexQuery.NET.Adapters.AgGrid;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Samples.WebApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

[ApiController]
[Route("api/aggrid/customers")]
[Produces("application/json")]
public sealed class AgGridController : ControllerBase
{
    private readonly AppDbContext _db;

    public AgGridController(AppDbContext db) => _db = db;

    [HttpPost]
    [ProducesResponseType(typeof(AgGridServerSideResponse), 200)]
    public async Task<IActionResult> GetCustomers(
        [FromBody] AgGridRequest request,
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
        var agGridResponse = result.ToAgGridServerSideResponse(request, camelCase: true);

        return Ok(new
        {
            rowData = agGridResponse.RowData,
            rowCount = agGridResponse.RowCount,
            __diagnostics = new
            {
                request = new
                {
                    startRow = request.StartRow,
                    endRow = request.EndRow,
                    filterModel = request.FilterModel,
                    sortModel = request.SortModel,
                    rowGroupCols = request.RowGroupCols,
                    groupKeys = request.GroupKeys,
                    valueCols = request.ValueCols
                },
                report = DiagnosticsHelper.BuildReportShape(report),
                provider = "EF Core",
                adapter = "AG Grid"
            }
        });
    }
}
