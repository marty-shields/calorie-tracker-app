using CalorieTracker.Core;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;

namespace CalorieTracker.UI.Tests;

/// <summary>Core's real <see cref="FavouriteService"/> over a temporary database, since it has no interface to fake. Dropping the Favourite table makes it fail.</summary>
public sealed class FavouriteDatabase : IAsyncDisposable
{
    private readonly string path;
    private readonly ServiceProvider core;

    private FavouriteDatabase(string path, ServiceProvider core, SQLiteAsyncConnection connection)
    {
        this.path = path;
        this.core = core;
        Connection = connection;
    }

    public SQLiteAsyncConnection Connection { get; }

    public FavouriteService Service => core.GetRequiredService<FavouriteService>();

    public static async Task<FavouriteDatabase> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"calorietracker-ui-fav-{Guid.NewGuid():N}.db3");
        var core = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(path)
            .BuildServiceProvider();
        await core.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        return new FavouriteDatabase(path, core, new SQLiteAsyncConnection(path));
    }

    /// <summary>Stores the food and returns its id.</summary>
    public async Task<int> InsertAsync(Food food)
    {
        await Connection.InsertAsync(food);
        return food.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.CloseAsync();
        await core.DisposeAsync();
        File.Delete(path);
    }
}
