namespace CalorieTracker.Core.Logging;

/// <summary>
/// Event ids of Core's log messages: 1000–1999, a block of 100 per type.
/// Ids never change and are never reused; list a retired id here and in <c>CoreEventTests</c>.
/// </summary>
internal enum CoreEvent
{
    /// <summary><see cref="Data.DatabaseInitializer"/>: stored and current schema versions.</summary>
    SchemaVersion = 1001,

    /// <summary><see cref="Data.DatabaseInitializer"/>: new database created.</summary>
    DatabaseCreated = 1002,

    /// <summary><see cref="Data.DatabaseInitializer"/>: a schema upgrade step ran.</summary>
    SchemaStepApplied = 1003,
}
