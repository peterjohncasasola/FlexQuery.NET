using BenchmarkDotNet.Attributes;
using FlexQuery.Benchmarks.Abstractions;
using FlexQuery.Benchmarks.Models;

using FlexQuery.NET;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.Benchmarks.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.ModelBuilder;
using Sieve.Services;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System;
using System.Linq;
using FlexQuery.Benchmarks.Infrastructure.Database;
using HotChocolate;
using Microsoft.EntityFrameworkCore;
using Sieve.Models;
using Gridify;
using HotChocolate.Types.Pagination;

namespace FlexQuery.Benchmarks.Benchmarks.Api;

public class ApiEndToEndBenchmarks : ApiBenchmarkBase
{
    [Params(20, 100, 1000, 100000)]
    public int PageSize { get; set; }

    protected override void ConfigureApiServices(IServiceCollection services)
    {
        // OData
        services.AddControllers()
            .AddOData(opt =>
            {
                var builder = new ODataConventionModelBuilder();
                builder.EntitySet<User>("UsersOData");
                opt.AddRouteComponents("odata", builder.GetEdmModel())
                    .Filter().Select().OrderBy().Count().SetMaxTop(100000);
            })
            .AddFlexQuerySecurity();

        // Sieve
        services.AddScoped<ISieveProcessor>(_ => SieveFactory.Create());

        // GraphQL
        services.AddGraphQLServer()
            .ModifyPagingOptions(opt => 
            { 
                opt.MaxPageSize = 100000;
                opt.DefaultPageSize = 20;
                opt.IncludeTotalCount = true;
            })
            .AddQueryType<Query>()
            .AddFiltering()
            .AddSorting()
            .AddProjections();

    }

    protected override void ConfigureApi(IWebHostBuilder webBuilder)
    {
        webBuilder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGraphQL();
            });
        });
    }

    [Benchmark(Baseline = true)]
    public async Task FlexQuery_Api()
    {
        var response = await Client.GetAsync($"/api/users/flexquery?filter=status:eq:active&sort=name:asc&page=1&pageSize={PageSize}&select=id,name,email");
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task OData_Api()
    {
        var response = await Client.GetAsync($"/odata/UsersOData?$filter=Status eq 'active'&$orderby=Name asc&$top={PageSize}&$select=Id,Name,Email");
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task GraphQL_Api()
    {
        var request = new { query = $$"""{ users(where: { status: { eq: "active" } }, order: { name: ASC }, take: {{PageSize}}) { items { id name email } } }""" };
        var response = await Client.PostAsJsonAsync("/graphql", request);
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task Gridify_Api()
    {
        var response = await Client.GetAsync($"/api/users/gridify?filter=status=active&orderBy=name asc&page=1&pageSize={PageSize}");
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task Sieve_Api()
    {
        var response = await Client.GetAsync($"/api/users/sieve?filters=Status==active&sorts=Name&page=1&pageSize={PageSize}");
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task ManualLinq_Api()
    {
        var response = await Client.GetAsync($"/api/users/manual?pageSize={PageSize}");
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();
    }
}

// Support classes moved from original ApiBenchmarks
public class Query
{
    [UseOffsetPaging(MaxPageSize = 100000, DefaultPageSize = 20, IncludeTotalCount = true)]

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<User> GetUsers(BenchmarkDbContext context) => context.Users.AsNoTracking();
}

public class UsersODataController : Microsoft.AspNetCore.OData.Routing.Controllers.ODataController
{
    private readonly BenchmarkDbContext _context;
    public UsersODataController(BenchmarkDbContext context) => _context = context;

    [Microsoft.AspNetCore.OData.Query.EnableQuery(MaxTop = 100000)]
    public IQueryable<User> Get() => _context.Users.AsNoTracking();
}

[Route("api/users")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly BenchmarkDbContext _context;
    public UsersController(BenchmarkDbContext context) => _context = context;

    [HttpGet("flexquery")]
    public async Task<IActionResult> FlexQuery([FromQuery] FlexQuery.NET.Models.FlexQueryParameters parameters)
    {
        var result = await _context.Users.AsNoTracking().FlexQueryAsync(parameters);
        return Ok(result);
    }

    [HttpGet("gridify")]
    public IActionResult Gridify([FromQuery] GridifyQuery parameters)
    {
        var result = _context.Users.AsNoTracking()
            .ApplyFiltering(parameters)
            .ApplyOrdering(parameters)
            .ApplyPaging(parameters)
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToList();
        return Ok(result);
    }

    [HttpGet("sieve")]
    public IActionResult Sieve([FromQuery] SieveModel parameters, [FromServices] ISieveProcessor sieveProcessor)
    {
        var result = sieveProcessor.Apply(parameters, _context.Users.AsNoTracking())
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToList();
        return Ok(result);
    }

    [HttpGet("manual")]
    public IActionResult Manual([FromQuery] int pageSize)
    {
        var result = _context.Users.AsNoTracking()
            .Where(u => u.Status == "active")
            .OrderBy(u => u.Name)
            .Take(pageSize)
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToList();
        return Ok(result);
    }
}
