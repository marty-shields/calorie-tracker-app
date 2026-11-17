using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Components.Actions;
using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Screens;

/// <summary>
/// What a screen's star button needs: whether the food is a favourite, and starring or unstarring it with a snackbar. Logs food ids and statuses only.
/// </summary>
internal sealed partial class FavouriteStar : IDisposable
{
    /// <summary>The text of the snackbar after a failed write.</summary>
    public const string FailedText = "Couldn't update favourites. Try again.";

    private readonly FavouriteService service;
    private readonly SnackbarService snackbar;
    private readonly ILogger logger;
    private readonly Action changed;
    private int foodId;
    private int epoch;
    private bool busy;
    private bool disposed;

    /// <param name="changed">Called when <see cref="IsFavourite"/> changes outside a tap, such as when Undo runs.</param>
    public FavouriteStar(FavouriteService service, SnackbarService snackbar, ILogger logger, Action changed)
    {
        this.service = service;
        this.snackbar = snackbar;
        this.logger = logger;
        this.changed = changed;
    }

    /// <summary>Null while unknown or when the read failed: the screen then shows no star.</summary>
    public bool? IsFavourite { get; private set; }

    /// <summary>The icon of the star button: filled for a favourite.</summary>
    public static IconName IconFor(bool favourite) => favourite ? IconName.StarFilled : IconName.Star;

    /// <summary>The accessible label of the star button.</summary>
    public static string LabelFor(bool favourite) => favourite ? "Remove from favourites" : "Add to favourites";

    /// <summary>Forgets the state and reads it for the food. An answer for a food the screen has left is dropped.</summary>
    public async Task LoadAsync(int id)
    {
        var mine = Reset(id);
        var result = await service.IsFavouriteAsync(id);
        if (disposed || epoch != mine)
        {
            return;
        }

        if (result.Status == FavouriteReadStatus.Found)
        {
            IsFavourite = result.IsFavourite;
            return;
        }

        Log.ReadFailed(logger, id);
    }

    /// <summary>Forgets the state, for a screen with no food to star.</summary>
    public void Clear() => Reset(0);

    private int Reset(int id)
    {
        foodId = id;
        IsFavourite = null;
        busy = false;
        return ++epoch;
    }

    /// <summary>
    /// Flips the star at once, then writes. A failed write puts the star back and says so. Starring says "Added to favourites";
    /// unstarring says "Removed from favourites" with Undo. A tap while a write runs, or with no state, does nothing.
    /// </summary>
    public async Task ToggleAsync()
    {
        if (busy || IsFavourite is not { } current)
        {
            return;
        }

        busy = true;
        var mine = epoch;
        try
        {
            IsFavourite = !current;
            if (!await WriteAsync(foodId, !current) && epoch == mine)
            {
                IsFavourite = current;
            }
        }
        finally
        {
            if (epoch == mine)
            {
                busy = false;
            }
        }
    }

    public void Dispose() => disposed = true;

    /// <summary>Writes and posts the snackbar. Returns false when the write failed (and says so). The Undo it posts may run after the screen is gone.</summary>
    private async Task<bool> WriteAsync(int id, bool star)
    {
        var status = (star ? await service.AddAsync(id) : await service.RemoveAsync(id)).Status;
        if (status is FavouriteStatus.Added or FavouriteStatus.AlreadyFavourite or FavouriteStatus.Removed or FavouriteStatus.NotFavourite)
        {
            if (star)
            {
                snackbar.Show("Added to favourites");
            }
            else
            {
                snackbar.Show("Removed from favourites", "Undo", () => UndoAsync(id));
            }

            return true;
        }

        Log.WriteFailed(logger, id, status);
        snackbar.Show(FailedText);
        return false;
    }

    private async Task UndoAsync(int id)
    {
        if (busy && foodId == id)
        {
            return;
        }

        var done = await WriteAsync(id, true);
        if (done && !disposed && foodId == id)
        {
            IsFavourite = true;
            changed();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.FavouriteReadFailed, LogLevel.Warning, "Reading whether food {FoodId} is a favourite failed")]
        public static partial void ReadFailed(ILogger logger, int foodId);

        [LoggerMessage((int)UiEvent.FavouriteWriteFailed, LogLevel.Warning, "Updating the favourite of food {FoodId} failed: {Status}")]
        public static partial void WriteFailed(ILogger logger, int foodId, FavouriteStatus status);
    }
}
