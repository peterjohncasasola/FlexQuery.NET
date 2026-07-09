using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Api.Dapper;

public abstract class DapperApiTestBase : IDisposable
{
    protected readonly IHost Host;
    protected readonly HttpClient Client;
    protected readonly IDbConnection Connection;

    protected DapperApiTestBase()
    {
        // Setup SQLite in-memory connection and seed it
        var db = SqlProjectionDbContext.CreateSeeded();
        Connection = db.Database.GetDbConnection();
        if (Connection.State != ConnectionState.Open) Connection.Open();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.UseStartup<DemoApiStartup>();
                webBuilder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(Connection);
                });
            })
            .Start();

        Client = Host.GetTestClient();
    }

    public void Dispose()
    {
        Connection.Close();
        Connection.Dispose();
        Client.Dispose();
        Host.Dispose();
    }
}
