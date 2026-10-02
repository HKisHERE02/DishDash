using DishDash.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
namespace DishDash.IntegrationTests;

public sealed class AppFactory(string environment = "Testing") : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"dishdash-tests-{Guid.NewGuid()}.db");
    private readonly string? postgresConnection = TestPostgresConnection();
    private bool initialized;
    private static string? TestPostgresConnection()
    {
        var configured = Environment.GetEnvironmentVariable("DISHDASH_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) return null;
        return new NpgsqlConnectionStringBuilder(configured) { Database = $"dishdash_test_{Guid.NewGuid():N}" }.ConnectionString;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DishDash.Api")));
        builder.UseEnvironment(environment);
        builder.UseSetting("Seed:Demo", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DishDashDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DishDashDbContext>>();
            services.AddDbContext<DishDashDbContext>(o =>
            {
                if (postgresConnection is null) o.UseSqlite($"Data Source={database}");
                else o.UseNpgsql(postgresConnection);
            });
        });
    }
    public async Task Initialize()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DishDashDbContext>();
        if (postgresConnection is null) await db.Database.EnsureCreatedAsync();
        else await db.Database.MigrateAsync();
        initialized = true;
        await DemoSeed.Initialize(db, scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(), null);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && initialized && postgresConnection is not null)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DishDashDbContext>();
            var name = new NpgsqlConnectionStringBuilder(postgresConnection).Database;
            if (name is null || !name.StartsWith("dishdash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing to remove a non-test database.");
            db.Database.EnsureDeleted();
        }
        base.Dispose(disposing);
        if (disposing) { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(database)) File.Delete(database); }
    }
}
