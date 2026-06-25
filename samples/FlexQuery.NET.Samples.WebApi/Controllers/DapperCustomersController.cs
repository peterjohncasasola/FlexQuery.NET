using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Diagnostics;
using FlexQuery.NET.Helpers;
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
public sealed class DapperCustomersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMappingRegistry _registry;

    public DapperCustomersController(AppDbContext db, IMappingRegistry registry)
    {
        _db = db;
        _registry = registry;
    }

    [HttpGet]
    [ProducesResponseType(typeof(QueryResult<Customer>), 200)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] FlexQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var options = QueryOptionsParser.Parse(parameters);
        var connection = _db.Database.GetDbConnection();

        object report;
        object? data;
        int? totalCount;
        int? page;
        int? pageSize;

        if (options.GroupBy is { Count: > 0 } || options.Aggregates.Count > 0)
        {
            options.Items["EntityType"] = typeof(Customer);
            var translator = new SqlTranslator(_registry, new SqliteDialect());
            var sqlCommand = translator.Translate(options);

            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync(cancellationToken);

            var dapperParams = new DynamicParameters();
            foreach (var param in sqlCommand.Parameters)
            {
                var cleanName = param.Key.TrimStart('@', ':', '?');
                dapperParams.Add(cleanName, param.Value);
            }

            var raw = (await connection.QueryAsync(sqlCommand.Sql, dapperParams)).ToList();
            if (raw.Count == 0)
            {
                data = raw;
            }
            else
            {
                var first = (IDictionary<string, object>)raw[0];
                var colTypes = first.Keys
                    .ToDictionary(k => k, _ => typeof(object), StringComparer.OrdinalIgnoreCase);
                var projectedType = DynamicTypeBuilder.GetDynamicType(
                    new Dictionary<string, Type>(colTypes));

                data = raw.Select(row =>
                {
                    var dict = (IDictionary<string, object>)row;
                    var instance = Activator.CreateInstance(projectedType)!;
                    foreach (var kvp in dict)
                    {
                        var prop = projectedType.GetProperty(kvp.Key);
                        if (prop is { CanWrite: true })
                            prop.SetValue(instance, kvp.Value);
                    }
                    return instance;
                }).ToList();
            }
            totalCount = null;
            page = null;
            pageSize = null;

            report = DiagnosticsHelper.BuildManualShape(
                sqlCommand.Sql, raw.Count, provider: "Dapper", translator: "Sqlite");
        }
        else
        {
            var collector = new FlexQueryDiagnosticsCollector();

            var result = await connection.FlexQueryAsync<Customer>(parameters,
                configureDapper: opt =>
                {
                    opt.Dialect = new SqliteDialect();
                    opt.MappingRegistry = _registry;
                    opt.EntityType = typeof(Customer);
                },
                configureExecution: cfg => cfg.Listener = collector);

            data = result.Data;
            totalCount = result.TotalCount;
            page = result.Page;
            pageSize = result.PageSize;

            report = DiagnosticsHelper.BuildReportShape(
                collector.BuildReport(provider: "Dapper", translator: "Sqlite"));
        }

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
