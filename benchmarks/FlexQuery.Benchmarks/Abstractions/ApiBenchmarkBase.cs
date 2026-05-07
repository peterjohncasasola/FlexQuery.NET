using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using System.Threading.Tasks;
using FlexQuery.Benchmarks.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FlexQuery.Benchmarks.Infrastructure.Seed;
using System;

namespace FlexQuery.Benchmarks.Abstractions;

/// <summary>
/// Base class for full ASP.NET Core pipeline benchmarks.
/// </summary>
public abstract class ApiBenchmarkBase : BenchmarkBase
{
    protected IHost Host = null!;
    protected HttpClient Client = null!;

    public override void Setup()
    {
        // We still need the DbContext for manual seeding/verification if needed,
        // but the TestServer will have its own scoped context.
        base.Setup();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    // Use a unique name for each run's DB
                    services.AddDbContext<BenchmarkDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName: "ApiBenchmarkDb"));

                    ConfigureApiServices(services);
                });
                ConfigureApi(webBuilder);
            })
            .Start();

        Client = Host.GetTestClient();
        
        // Seed the API's database
        using var scope = Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
        DataSeeder.Seed(db, 1000);
    }

    protected abstract void ConfigureApiServices(IServiceCollection services);
    protected virtual void ConfigureApi(IWebHostBuilder webBuilder) { }

    public override void Cleanup()
    {
        Client.Dispose();
        Host.Dispose();
        base.Cleanup();
    }
}
