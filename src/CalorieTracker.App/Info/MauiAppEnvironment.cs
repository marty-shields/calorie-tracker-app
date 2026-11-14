using CalorieTracker.Core.Services;

namespace CalorieTracker.App.Info;

/// <summary>The app's display version, from MAUI.</summary>
internal sealed class MauiAppEnvironment : IAppEnvironment
{
    /// <inheritdoc />
    public string AppVersion => AppInfo.Current.VersionString;
}
