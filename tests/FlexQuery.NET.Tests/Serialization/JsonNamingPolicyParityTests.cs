using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Serialization;

public class JsonNamingPolicyParityTests : IAsyncLifetime
{
    private readonly DbConnection _connection;
    private IHost _pascalHost = null!;
    private IHost _camelHost = null!;
    private HttpClient _pascalClient = null!;
    private HttpClient _camelClient = null!;

    public JsonNamingPolicyParityTests()
    {
        var db = SqlProjectionDbContext.CreateSeeded();
        _connection = db.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
    }

    public async Task InitializeAsync()
    {
    _pascalHost = Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseTestServer();
            webBuilder.UseStartup<DemoApiStartup>();
            webBuilder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IDbConnection>(_connection);
            });
        })
        .Start();
    _pascalClient = _pascalHost.GetTestClient();

    _camelHost = Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseTestServer();
            webBuilder.UseStartup<CamelCaseStartup>();
            webBuilder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IDbConnection>(_connection);
            });
        })
            .Start();
        _camelClient = _camelHost.GetTestClient();
    }

    public Task DisposeAsync()
    {
        _pascalClient.Dispose();
        _pascalHost.Dispose();
        _camelClient.Dispose();
        _camelHost.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("CustomerId")]
    [InlineData("customerId")]
    [InlineData("CUSTOMERID")]
    public async Task DefaultSerializer_SelectCasing_UsesPascalCase(string field)
    {
        var response = await _pascalClient.GetAsync($"/api/orders?select={field}&sort=Id");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("TotalCount");
        root.GetProperty("ResultCount");
        root.GetProperty("Page");
        root.GetProperty("PageSize");
        root.GetProperty("Data");
        root.GetProperty("Data")[0].GetProperty("CustomerId");
    }

    [Theory]
    [InlineData("CustomerId")]
    [InlineData("customerId")]
    [InlineData("CUSTOMERID")]
    public async Task CamelCaseSerializer_SelectCasing_UsesCamelCase(string field)
    {
        var response = await _camelClient.GetAsync($"/api/orders?select={field}&sort=Id");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("totalCount");
        root.GetProperty("resultCount");
        root.GetProperty("page");
        root.GetProperty("pageSize");
        root.GetProperty("data");
        root.GetProperty("data")[0].GetProperty("customerId");
    }

    [Fact]
    public async Task DefaultSerializer_MultipleFields_AllUsePascalCase()
    {
        var response = await _pascalClient.GetAsync("/api/orders?select=customerId,ORDERDATE&sort=Id");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement.GetProperty("Data")[0];

        doc.RootElement.GetProperty("TotalCount");
        item.GetProperty("CustomerId");
        item.GetProperty("OrderDate");
    }

    [Fact]
    public async Task CamelCaseSerializer_MultipleFields_AllUseCamelCase()
    {
        var response = await _camelClient.GetAsync("/api/orders?select=customerId,ORDERDATE&sort=Id");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement.GetProperty("data")[0];

        doc.RootElement.GetProperty("totalCount");
        item.GetProperty("customerId");
        item.GetProperty("orderDate");
    }

    [Fact]
    public async Task DefaultSerializer_GroupBy_UsesPascalCaseForGroupField()
    {
        var response = await _pascalClient.GetAsync("/api/orders?groupBy=customerId&aggregate=sum:total:totalSum&sort=CustomerId:asc");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement.GetProperty("Data")[0];

        doc.RootElement.GetProperty("TotalCount");
        item.GetProperty("CustomerId");
        item.GetProperty("totalSum");
    }

    [Fact]
    public async Task CamelCaseSerializer_GroupBy_UsesCamelCaseForGroupField()
    {
        var response = await _camelClient.GetAsync("/api/orders?groupBy=customerId&aggregate=sum:total:totalSum&sort=CustomerId:asc");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement.GetProperty("data")[0];

        doc.RootElement.GetProperty("totalCount");
        item.GetProperty("customerId");
        item.GetProperty("totalSum");
    }

    [Fact]
    public async Task DefaultSerializer_NestedProjection_UsesPascalCaseAtEveryLevel()
    {
        var response = await _pascalClient.GetAsync("/api/users?select=id,orders.orderDate,orders.STATUS&include=Orders&expand=Orders&sort=Id");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var customer = doc.RootElement.GetProperty("Data")[0];

        doc.RootElement.GetProperty("TotalCount");
        customer.GetProperty("Id");
        customer.GetProperty("Orders");

        var order = customer.GetProperty("Orders")[0];
        order.GetProperty("OrderDate");
        order.GetProperty("Status");
    }

    [Fact]
    public async Task CamelCaseSerializer_NestedProjection_UsesCamelCaseAtEveryLevel()
    {
        var response = await _camelClient.GetAsync("/api/users?select=id,orders.orderDate,orders.STATUS&include=Orders&expand=Orders&sort=Id");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var customer = doc.RootElement.GetProperty("data")[0];

        doc.RootElement.GetProperty("totalCount");
        customer.GetProperty("id");
        customer.GetProperty("orders");

        var order = customer.GetProperty("orders")[0];
        order.GetProperty("orderDate");
        order.GetProperty("status");
    }

    [Fact]
    public async Task DefaultSerializer_Alias_IsPreserved()
    {
        var response = await _pascalClient.GetAsync("/api/users?select=name:CompanyName");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Data")[0].GetProperty("CompanyName");
    }

    [Fact]
    public async Task CamelCaseSerializer_Alias_IsCamelCased()
    {
        var response = await _camelClient.GetAsync("/api/users?select=name:CompanyName");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("data")[0].GetProperty("companyName");
    }
}

public class CamelCaseStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(DemoApiStartup).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            })
            .AddFlexQuerySecurity();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
