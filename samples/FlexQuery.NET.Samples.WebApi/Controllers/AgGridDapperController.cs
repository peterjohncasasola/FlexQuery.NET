using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FlexQuery.NET.Adapters.AgGrid;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Samples.WebApi.Data;
using FlexQuery.NET.Samples.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Samples.WebApi.Controllers;

[ApiController]
[Route("api/aggrid-dapper/customers")]
[Produces("application/json")]
public sealed class AgGridDapperController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMappingRegistry _registry;

    public AgGridDapperController(AppDbContext db, IMappingRegistry registry)
    {
        _db = db;
        _registry = registry;
    }

    [HttpPost]
    [ProducesResponseType(typeof(AgGridServerSideResponse), 200)]
    public async Task<IActionResult> GetCustomers(
        [FromBody] AgGridRequest request,
        CancellationToken cancellationToken)
    {
        var options = request.ToQueryOptions();
        options.IncludeCount = true;
        options.Items["EntityType"] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqliteDialect());

        string? generatedSql = null;
        IReadOnlyList<Customer> rows = [];
        int total = 0;

        try
        {
            var dataCommand = translator.Translate(options);
            generatedSql = dataCommand.Sql;

            var countOptions = options.Clone();
            countOptions.Paging = new PagingOptions { Disabled = true };
            var countCommand = translator.Translate(countOptions);

            var connection = _db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await ((DbConnection)connection).OpenAsync(cancellationToken);

            var dataParams = new DynamicParameters();
            foreach (var p in dataCommand.Parameters)
                dataParams.Add(p.Key, p.Value);

            var countParams = new DynamicParameters();
            foreach (var p in countCommand.Parameters)
                countParams.Add(p.Key, p.Value);

            rows = (await connection.QueryAsync<Customer>(dataCommand.Sql, dataParams)).AsList();
            total = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM ({countCommand.Sql}) t", countParams);
        }
        catch
        {
        }

        var queryResult = new QueryResult<Customer>
        {
            Data = rows,
            TotalCount = total,
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
                    generatedSql, rows.Count, provider: "Dapper", translator: "Sqlite"),
                provider = "Dapper",
                adapter = "AG Grid"
            }
        });
    }
}
