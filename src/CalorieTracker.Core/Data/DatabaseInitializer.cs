using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Upgrades the schema version stored in SQLite <c>user_version</c>, then creates missing tables.</summary>
internal sealed partial class DatabaseInitializer : IDatabaseInitializer
{
    public const int CurrentVersion = 5;

    private readonly SQLiteAsyncConnection connection;
    private readonly IReadOnlyList<ISchemaUpgrade> upgrades;
    private readonly ILogger<DatabaseInitializer> logger;
    private readonly int currentVersion;

    /// <summary>Uses <see cref="CurrentVersion"/>.</summary>
    public DatabaseInitializer(SQLiteAsyncConnection connection, IEnumerable<ISchemaUpgrade> upgrades, ILogger<DatabaseInitializer> logger)
        : this(connection, upgrades, logger, CurrentVersion)
    {
    }

    internal DatabaseInitializer(
        SQLiteAsyncConnection connection,
        IEnumerable<ISchemaUpgrade> upgrades,
        ILogger<DatabaseInitializer> logger,
        int currentVersion)
    {
        this.connection = connection;
        this.upgrades = upgrades.ToList();
        this.logger = logger;
        this.currentVersion = currentVersion;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Throws <see cref="InvalidOperationException"/>, before changing anything, if the stored version is newer than the app's
    /// or the steps are not exactly one for each version from 2 to the app's.
    /// A new database (version 0) gets the current schema from the tables and runs no steps.
    /// An existing database runs the steps after its stored version, then gets any missing tables and columns.
    /// Tables are created where the comments say, with <c>connection.CreateTablesAsync</c> for the entity types.
    /// </remarks>
    public async Task InitializeAsync()
    {
        var stored = await connection.ExecuteScalarAsync<int>("PRAGMA user_version");
        Log.SchemaVersion(logger, stored, currentVersion);
        if (stored > currentVersion)
        {
            throw new InvalidOperationException($"Database schema version {stored} is newer than the app's version {currentVersion}");
        }

        var steps = OrderedSteps();

        if (stored == 0)
        {
            // Create the tables of a new database here.
            await connection.CreateTableAsync<Food>();
            await connection.CreateTableAsync<FoodBarcode>();
            await connection.CreateTableAsync<SeedVersion>();
            await connection.ExecuteAsync($"PRAGMA user_version = {currentVersion}");
            Log.DatabaseCreated(logger, currentVersion);
        }
        else
        {
            foreach (var step in steps.Where(s => s.Version > stored))
            {
                await connection.RunInTransactionAsync(db =>
                {
                    step.Apply(db);
                    db.Execute($"PRAGMA user_version = {step.Version}");
                });
                Log.StepApplied(logger, step.Version);
            }

            // Create any table or column an existing database lacks here.
            await connection.CreateTableAsync<Food>();
            await connection.CreateTableAsync<FoodBarcode>();
            await connection.CreateTableAsync<SeedVersion>();
        }
    }

    private List<ISchemaUpgrade> OrderedSteps()
    {
        var steps = upgrades.OrderBy(u => u.Version).ToList();
        if (!steps.Select(s => s.Version).SequenceEqual(Enumerable.Range(2, currentVersion - 1)))
        {
            throw new InvalidOperationException(
                $"Expected one schema upgrade step for each version from 2 to {currentVersion}, found [{string.Join(", ", steps.Select(s => s.Version))}]");
        }

        return steps;
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.SchemaVersion, LogLevel.Information, "Database schema version {Stored}, app schema version {Current}")]
        public static partial void SchemaVersion(ILogger logger, int stored, int current);

        [LoggerMessage((int)CoreEvent.DatabaseCreated, LogLevel.Information, "New database created at schema version {Version}")]
        public static partial void DatabaseCreated(ILogger logger, int version);

        [LoggerMessage((int)CoreEvent.SchemaStepApplied, LogLevel.Information, "Schema upgrade step {Version} applied")]
        public static partial void StepApplied(ILogger logger, int version);
    }
}
