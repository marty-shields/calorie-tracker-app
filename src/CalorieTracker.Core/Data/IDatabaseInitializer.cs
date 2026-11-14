namespace CalorieTracker.Core.Data;

/// <summary>Prepares the database at startup.</summary>
public interface IDatabaseInitializer
{
    /// <summary>Runs the schema upgrade steps, then creates missing tables and columns.</summary>
    Task InitializeAsync();
}
