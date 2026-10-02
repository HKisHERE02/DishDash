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

public class AppFactory(string environment = "Testing") : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"dishdash-tests-{Guid.NewGuid()}.db");
    private readonly string? postgresConnection = TestPostgresConnection();
    private bool databaseInitializationStarted;
    private Task? cleanupTask;
    private Task? disposalTask;
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
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DishDashDbContext>();
        // Migrations can create the database before failing to create its schema.
        databaseInitializationStarted = true;
        if (postgresConnection is null) await db.Database.EnsureCreatedAsync();
        else await db.Database.MigrateAsync();
        await DemoSeed.Initialize(db, scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(), null);
    }

    public Task CleanupDatabaseAsync() => cleanupTask ??= CleanupDatabaseCoreAsync();

    private async Task CleanupDatabaseCoreAsync()
    {
        if (!databaseInitializationStarted) return;
        if (postgresConnection is not null)
        {
            var name = new NpgsqlConnectionStringBuilder(postgresConnection).Database;
            if (name is null || !name.StartsWith("dishdash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing to remove a non-test database.");
        }
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DishDashDbContext>();
        await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
    }

    public override ValueTask DisposeAsync() => new(disposalTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        try { await CleanupDatabaseAsync().ConfigureAwait(false); }
        finally { await base.DisposeAsync().ConfigureAwait(false); }
    }
}
