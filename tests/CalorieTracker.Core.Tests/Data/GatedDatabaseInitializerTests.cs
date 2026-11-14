using CalorieTracker.Core.Data;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

public class GatedDatabaseInitializerTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private GatedDatabaseInitializer initializer = null!;

    [SetUp]
    public void SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        gate = new DatabaseGate();
        initializer = new GatedDatabaseInitializer(new DatabaseInitializer(connection, [new FoodBarcodeUpgrade()], NullLogger<DatabaseInitializer>.Instance), gate);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task InitializeAsync_PassesThroughToTheInitializer()
    {
        await initializer.InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_WhileTheGateIsHeld_WaitsForIt()
    {
        var release = new TaskCompletionSource();
        var holder = gate.RunAsync(() => release.Task);

        var pending = initializer.InitializeAsync();

        Assert.That(pending.IsCompleted, Is.False);
        release.SetResult();
        await Task.WhenAll(holder, pending);
    }
}
