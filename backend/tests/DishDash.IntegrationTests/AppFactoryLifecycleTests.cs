using System.Data.Common;
using DishDash.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DishDash.IntegrationTests;

public sealed class AppFactoryLifecycleTests
{
    [Fact]
    public async Task ExplicitCleanupDeletesDatabaseWhileServicesAreStillAlive()
    {
        await using var factory = new AppFactory();
        await factory.Initialize();
        var database = DatabaseSnapshot.Capture(factory);
        Assert.True(await database.Exists());

        var cleanup = factory.CleanupDatabaseAsync();
        await cleanup;
        Assert.Same(cleanup, factory.CleanupDatabaseAsync());
        Assert.False(await database.Exists());
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DishDashDbContext>());
    }

    [Fact]
    public async Task AsyncDisposalCleansDatabaseAndRepeatedCallsDoNotAccessDisposedServices()
    {
        await using var factory = new AppFactory();
        await factory.Initialize();
        var database = DatabaseSnapshot.Capture(factory);
        var services = factory.Services;
        Assert.True(await database.Exists());

        var disposal = factory.DisposeAsync().AsTask();
        await disposal;
        Assert.Same(disposal, factory.DisposeAsync().AsTask());
        await factory.CleanupDatabaseAsync();
        Assert.False(await database.Exists());
        Assert.Throws<ObjectDisposedException>(() => services.CreateScope());
    }

    [Fact]
    public async Task SynchronousDisposalUsesTheSameCleanupWithoutReenteringServices()
    {
        await using var factory = new AppFactory();
        await factory.Initialize();
        var database = DatabaseSnapshot.Capture(factory);
        Assert.True(await database.Exists());

        factory.Dispose();
        factory.Dispose();
        await factory.DisposeAsync();
        Assert.False(await database.Exists());
    }

    [Fact]
    public async Task FailedSchemaInitializationStillDeletesThePartiallyCreatedDatabase()
    {
        await using var factory = new FailingSchemaFactory();
        var database = DatabaseSnapshot.Capture(factory);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => factory.Initialize());
        Assert.Equal("Simulated schema creation failure.", error.Message);
        Assert.True(await database.Exists());

        await factory.DisposeAsync();
        Assert.False(await database.Exists());
    }

    [Fact]
    public async Task DisposingAnUnusedFactoryDoesNotStartTheHost()
    {
        await using var factory = new UnstartedFactory();
        await factory.CleanupDatabaseAsync();
        await factory.DisposeAsync();
        factory.Dispose();
    }

    [Fact]
    public async Task CleanupOnlyDeletesItsOwnIsolatedDatabase()
    {
        await using var first = new AppFactory();
        await using var second = new AppFactory();
        await first.Initialize();
        await second.Initialize();
        var firstDatabase = DatabaseSnapshot.Capture(first);
        var secondDatabase = DatabaseSnapshot.Capture(second);
        Assert.NotEqual(firstDatabase.Name, secondDatabase.Name);

        await first.DisposeAsync();
        Assert.False(await firstDatabase.Exists());
        Assert.True(await secondDatabase.Exists());
        using (var scope = second.Services.CreateScope())
            Assert.Equal(20, await scope.ServiceProvider.GetRequiredService<DishDashDbContext>().Dishes.CountAsync());

        await second.DisposeAsync();
        Assert.False(await secondDatabase.Exists());
    }

    private sealed record DatabaseSnapshot(string ConnectionString, bool Postgres)
    {
        public string Name => Postgres
            ? new NpgsqlConnectionStringBuilder(ConnectionString).Database!
            : new SqliteConnectionStringBuilder(ConnectionString).DataSource;

        public static DatabaseSnapshot Capture(AppFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DishDashDbContext>();
            var snapshot = new DatabaseSnapshot(db.Database.GetConnectionString()!, db.Database.IsNpgsql());
            if (snapshot.Postgres) Assert.StartsWith("dishdash_test_", snapshot.Name);
            return snapshot;
        }

        public async Task<bool> Exists()
        {
            if (!Postgres) return File.Exists(Name);
            var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
            command.Parameters.AddWithValue("name", Name);
            return (bool)(await command.ExecuteScalarAsync())!;
        }
    }

    private sealed class FailingSchemaFactory : AppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddDbContext<DishDashDbContext>(options => options.AddInterceptors(new FailSchemaCreation())));
        }
    }

    private sealed class FailSchemaCreation : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Simulated schema creation failure.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class UnstartedFactory : AppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => throw new InvalidOperationException("Disposal must not start the host.");
    }
}
