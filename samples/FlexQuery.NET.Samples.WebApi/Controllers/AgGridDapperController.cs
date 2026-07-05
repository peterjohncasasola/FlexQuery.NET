using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FlexQuery.NET;
using FlexQuery.NET.Adapters.AgGrid;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Extensions;
using FlexQuery.NET.Samples.WebApi.Data;
using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

[ApiController]
[Route("api/aggrid-dapper/customers")]
[Produces("application/json")]
public sealed class AgGridDapperController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AgGridServerSideResponse), 200)]
    public async Task<IActionResult> GetCustomers(
        [FromBody] AgGridRequest request,
        CancellationToken cancellationToken)
    {
        var options = request.ToQueryOptions();
        options.IncludeCount = true;

        string? generatedSql = null;
        QueryResult<object>? result = null;

        try
        {
            var connectionString = db.Database.GetConnectionString();
            var connection = new SqliteConnection(connectionString);
            
            result = await connection.FlexQueryAsync<Customer>(options, cancellationToken: cancellationToken);
            
        } 
        catch
        {
        }

        var queryResult = new QueryResult<object>
        {
            Data = result!.Data,
            TotalCount = result.TotalCount,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize
        };
        var agGridResponse = queryResult.ToAgGridServerSideResponse(request, camelCase: true);

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
                report = DiagnosticsHelper.BuildManualShape(
                    generatedSql, result.TotalCount, provider: "Dapper", translator: "Sqlite"),
                provider = "Dapper",
                adapter = "AG Grid"
            }
        });
    }
}
