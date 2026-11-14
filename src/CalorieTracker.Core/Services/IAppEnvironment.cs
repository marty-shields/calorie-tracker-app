namespace CalorieTracker.Core.Services;

/// <summary>Values from the host that Core and the UI need. Implemented by the host.</summary>
public interface IAppEnvironment
{
    /// <summary>The app's display version, such as 1.4.2.</summary>
    string AppVersion { get; }
}
