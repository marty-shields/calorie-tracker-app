using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

public class DatabaseInitializerTests
{
    private readonly List<string> calls = [];
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeLogger<DatabaseInitializer> logger = null!;

    [SetUp]
    public void SetUp()
    {
        calls.Clear();
        logger = new FakeLogger<DatabaseInitializer>();
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_SetsTheCurrentVersionWithoutRunningSteps()
    {
        await Initializer(2, Step(2)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(2));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_NewDatabaseAtTheAppsVersion_NeedsNoSteps()
    {
        await Initializer(DatabaseInitializer.CurrentVersion).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_OlderDatabase_RunsTheMissingStepsInOrder()
    {
        await SetVersion(1);

        await Initializer(3, Step(3), Step(2)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(3));
        Assert.That(calls, Is.EqualTo(new[] { "step 2", "step 3" }));
    }

    [Test]
    public async Task InitializeAsync_OlderDatabase_RunsOnlyTheStepsAfterTheStoredVersion()
    {
        await SetVersion(2);

        await Initializer(3, Step(2), Step(3)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(3));
        Assert.That(calls, Is.EqualTo(new[] { "step 3" }));
    }

    [Test]
    public async Task InitializeAsync_CurrentDatabase_RunsNoSteps()
    {
        await SetVersion(2);

        await Initializer(2, Step(2)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(2));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_LogsTheVersionsAndTheNewDatabase()
    {
        await Initializer(2, Step(2)).InitializeAsync();

        Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[]
        {
            (CoreEvent.SchemaVersion, LogLevel.Information, "Database schema version 0, app schema version 2"),
            (CoreEvent.DatabaseCreated, LogLevel.Information, "New database created at schema version 2"),
        }));
    }

    [Test]
    public async Task InitializeAsync_OlderDatabase_LogsTheVersionsAndEachStep()
    {
        await SetVersion(1);

        await Initializer(3, Step(2), Step(3)).InitializeAsync();

        Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[]
        {
            (CoreEvent.SchemaVersion, LogLevel.Information, "Database schema version 1, app schema version 3"),
            (CoreEvent.SchemaStepApplied, LogLevel.Information, "Schema upgrade step 2 applied"),
            (CoreEvent.SchemaStepApplied, LogLevel.Information, "Schema upgrade step 3 applied"),
        }));
    }

    [Test]
    public async Task InitializeAsync_NewerDatabase_ThrowsAndChangesNothing()
    {
        await SetVersion(3);

        Assert.That(() => Initializer(2, Step(2)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(3));
        Assert.That(calls, Is.Empty);
        Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[]
        {
            (CoreEvent.SchemaVersion, LogLevel.Information, "Database schema version 3, app schema version 2"),
        }));
    }

    [Test]
    public async Task InitializeAsync_MissingStep_ThrowsAndChangesNothing()
    {
        await SetVersion(1);

        Assert.That(() => Initializer(3, Step(3)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(1));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_DuplicateStep_ThrowsAndChangesNothing()
    {
        await SetVersion(1);

        Assert.That(() => Initializer(2, Step(2), Step(2)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(1));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_StepAboveTheCurrentVersion_ThrowsAndChangesNothing()
    {
        await SetVersion(1);

        Assert.That(() => Initializer(2, Step(2), Step(3)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(1));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_NewDatabaseWithAMissingStep_ThrowsAndChangesNothing()
    {
        Assert.That(() => Initializer(3, Step(3)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.Zero);
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_FailingStep_RollsBackThatStepOnly()
    {
        await SetVersion(1);
        var failing = Step(3, db =>
        {
            db.Execute("INSERT INTO Marker (Id) VALUES (1)");
            throw new InvalidOperationException("Step failed");
        });

        Assert.That(() => Initializer(3, Step(2, db => db.Execute("CREATE TABLE Marker (Id INTEGER)")), failing).InitializeAsync(),
            Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(2));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Marker"), Is.Zero);
        Assert.That(calls, Is.EqualTo(new[] { "step 2", "step 3" }));
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_CreatesTheFoodTableAndIndexes()
    {
        await Initializer(DatabaseInitializer.CurrentVersion).InitializeAsync();

        await AssertFoodTableAndIndexes();
    }

    [Test]
    public async Task InitializeAsync_ExistingDatabaseWithoutFood_CreatesTheFoodTableAndIndexes()
    {
        await SetVersion(1);

        await Initializer(DatabaseInitializer.CurrentVersion).InitializeAsync();

        await AssertFoodTableAndIndexes();
    }

    private async Task AssertFoodTableAndIndexes()
    {
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Food'"), Is.EqualTo(1));
        var unique = await connection.QueryScalarsAsync<string>("SELECT name FROM pragma_index_list('Food') WHERE \"unique\" = 1");
        var all = await connection.QueryScalarsAsync<string>("SELECT name FROM pragma_index_list('Food')");
        Assert.That(unique, Does.Contain("UX_Food_Source_SourceId"));
        Assert.That(all, Does.Contain("Food_Barcode"));
    }

    private DatabaseInitializer Initializer(int currentVersion, params ISchemaUpgrade[] steps) =>
        new(connection, steps, logger, currentVersion);

    private FakeStep Step(int version, Action<SQLiteConnection>? apply = null) => new(version, db =>
    {
        calls.Add($"step {version}");
        apply?.Invoke(db);
    });

    private Task SetVersion(int version) => connection.ExecuteAsync($"PRAGMA user_version = {version}");

    private Task<int> Version() => connection.ExecuteScalarAsync<int>("PRAGMA user_version");

    private sealed class FakeStep(int version, Action<SQLiteConnection> apply) : ISchemaUpgrade
    {
        public int Version => version;

        public void Apply(SQLiteConnection db) => apply(db);
    }
}
