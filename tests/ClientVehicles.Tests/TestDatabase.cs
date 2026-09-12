using ClientVehicles.Web.Data;
using ClientVehicles.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClientVehicles.Tests;

/// <summary>
/// A real SQLite database held in memory, created by running the actual EF migrations.
/// The in-memory provider is deliberately not used: it ignores unique indexes and check
/// constraints, which are exactly the guarantees these tests are about.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(_options);
        Db.Database.Migrate();

        Vehicles = new VehicleService(Db, TimeProvider.System);
        Clients = new ClientService(Db, Vehicles, TimeProvider.System);
    }

    public AppDbContext Db { get; }

    public ClientService Clients { get; }

    public VehicleService Vehicles { get; }

    /// <summary>A second context over the same database, to verify what was really persisted.</summary>
    public AppDbContext NewContext() => new(_options);

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
