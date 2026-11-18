using CalorieTracker.Core.Data;
using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CalorieTracker.UI.Tests.Screens;

public class FavouriteStarTests
{
    private FavouriteDatabase favourites = null!;
    private SnackbarService snackbar = null!;
    private FavouriteStar star = null!;
    private int changedCalls;
    private int foodId;

    [SetUp]
    public async Task SetUp()
    {
        favourites = await FavouriteDatabase.CreateAsync();
        snackbar = new SnackbarService(new FakeTimeProvider());
        star = new FavouriteStar(favourites.Service, snackbar, NullLogger.Instance, () => changedCalls++);
        foodId = await favourites.InsertAsync(new Food { Source = FoodSource.OpenFoodFacts, SourceId = "oats", Name = "Oats", Unit = FoodUnit.Gram, Kcal = 375, SearchText = "oats" });
    }

    [TearDown]
    public async Task TearDown()
    {
        star.Dispose();
        snackbar.Dispose();
        await favourites.DisposeAsync();
    }

    [Test]
    public async Task ATapWhileAWriteRuns_DoesNothing()
    {
        await star.LoadAsync(foodId);

        var release = favourites.HoldGate();
        var first = star.ToggleAsync();
        var second = star.ToggleAsync();
        Assert.That(first.IsCompleted, Is.False);
        Assert.That(second.IsCompleted, Is.True);
        await release();
        await first;

        Assert.That(star.IsFavourite, Is.True);
        Assert.That((await favourites.Service.IsFavouriteAsync(foodId)).IsFavourite, Is.True);
        Assert.That(snackbar.Current?.Text, Is.EqualTo("Added to favourites"));
    }

    [Test]
    public async Task AReadForAFoodTheScreenLeft_IsDropped()
    {
        await favourites.Service.AddAsync(foodId);

        var read = star.LoadAsync(foodId);
        star.Clear();
        await read;

        Assert.That(star.IsFavourite, Is.Null);
    }

    [Test]
    public async Task AFailedWriteForAFoodTheScreenLeft_DoesNotRestoreTheOldStar()
    {
        await star.LoadAsync(foodId);
        await favourites.Connection.ExecuteAsync("DROP TABLE Favourite");

        var toggle = star.ToggleAsync();
        star.Clear();
        await toggle;

        Assert.That(star.IsFavourite, Is.Null);
    }

    [Test]
    public async Task UndoWhileAWriteRuns_IsIgnored()
    {
        await favourites.Service.AddAsync(foodId);
        await star.LoadAsync(foodId);
        await star.ToggleAsync();
        var undo = snackbar.Current!;

        var release = favourites.HoldGate();
        var toggle = star.ToggleAsync();
        var undoing = snackbar.InvokeActionAsync(undo);
        Assert.That(undoing.IsCompleted, Is.True);
        await release();
        await toggle;

        Assert.That(changedCalls, Is.Zero);
        Assert.That(star.IsFavourite, Is.True);
    }
}
