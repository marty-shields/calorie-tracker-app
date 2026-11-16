using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>Whether the first-use notice of Open Food Facts search was shown.</summary>
public interface IOnlineSearchNotice
{
    /// <summary>Whether the notice was shown before. False when it was not, or when reading failed.</summary>
    Task<bool> WasShownAsync();

    /// <summary>Saves that the notice was shown.</summary>
    Task MarkShownAsync();
}

/// <summary>Remembers that the first-use notice of Open Food Facts search ("This sends what you typed to Open Food Facts.") was shown, in <see cref="AppSettings"/>. A failing database never throws: the notice then shows again.</summary>
public sealed partial class OnlineSearchNotice : IOnlineSearchNotice
{
    private readonly NoticeStore store;
    private readonly ILogger<OnlineSearchNotice> logger;

    internal OnlineSearchNotice(NoticeStore store, ILogger<OnlineSearchNotice> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    /// <summary>The words of the notice.</summary>
    public const string Text = "This sends what you typed to Open Food Facts.";

    public async Task<bool> WasShownAsync()
    {
        try
        {
            return await store.OnlineSearchSeenAsync();
        }
        catch (Exception)
        {
            Log.ReadFailed(logger);
            return false;
        }
    }

    public async Task MarkShownAsync()
    {
        try
        {
            await store.SetOnlineSearchSeenAsync();
        }
        catch (Exception)
        {
            Log.SaveFailed(logger);
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.OnlineNoticeReadFailed, LogLevel.Warning, "Reading whether the search notice was shown failed")]
        public static partial void ReadFailed(ILogger logger);

        [LoggerMessage((int)CoreEvent.OnlineNoticeSaveFailed, LogLevel.Warning, "Saving that the search notice was shown failed")]
        public static partial void SaveFailed(ILogger logger);
    }
}
