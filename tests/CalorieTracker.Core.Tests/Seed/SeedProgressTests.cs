using CalorieTracker.Core.Seed;

namespace CalorieTracker.Core.Tests.Seed;

public class SeedProgressTests
{
    private static readonly SeedLoadResult Loaded = new(SeedLoadStatus.Loaded, 1, 3, 0);
    private static readonly SeedLoadResult UpToDate = new(SeedLoadStatus.UpToDate, 1, 0, 0);
    private static readonly SeedLoadResult Failed = new(SeedLoadStatus.Failed, 0, 0, 0);

    [Test]
    public void BeforeStart_ThereIsNoLoad()
    {
        var progress = new SeedProgress(new FakeLoader());

        Assert.Multiple(() =>
        {
            Assert.That(progress.Current, Is.Null);
        });
    }

    [Test]
    public async Task Start_CompletesWithTheLoadersResult()
    {
        var loader = new FakeLoader();
        var progress = new SeedProgress(loader);

        var task = progress.Start();
        loader.Complete(Loaded);
        var result = await task;

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(Loaded));
            Assert.That(progress.Current, Is.SameAs(task));
        });
    }

    [Test]
    public async Task Start_UpToDate_CompletesWithThatStatus()
    {
        var loader = new FakeLoader();
        var progress = new SeedProgress(loader);

        var task = progress.Start();
        loader.Complete(UpToDate);

        Assert.That((await task).Status, Is.EqualTo(SeedLoadStatus.UpToDate));
    }

    [Test]
    public async Task Start_WhileLoading_ReturnsTheRunningLoad()
    {
        var loader = new FakeLoader();
        var progress = new SeedProgress(loader);

        var first = progress.Start();
        var second = progress.Start();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(loader.Calls, Is.EqualTo(1));
        });

        loader.Complete(Loaded);
        await first;
    }

    [Test]
    public async Task Start_AfterAFailure_StartsANewLoad()
    {
        var loader = new FakeLoader();
        var progress = new SeedProgress(loader);
        var first = progress.Start();
        loader.Complete(Failed);
        var failed = await first;

        var retry = progress.Start();
        loader.Complete(Loaded);
        var retried = await retry;

        Assert.Multiple(() =>
        {
            Assert.That(failed.Status, Is.EqualTo(SeedLoadStatus.Failed));
            Assert.That(retry, Is.Not.SameAs(first));
            Assert.That(loader.Calls, Is.EqualTo(2));
            Assert.That(retried.Status, Is.EqualTo(SeedLoadStatus.Loaded));
        });
    }

    private sealed class FakeLoader : ISeedLoader
    {
        private TaskCompletionSource<SeedLoadResult> pending = new();

        public int Calls { get; private set; }

        public Task<SeedLoadResult> LoadAsync()
        {
            Calls++;
            pending = new TaskCompletionSource<SeedLoadResult>();
            return pending.Task;
        }

        public void Complete(SeedLoadResult result) => pending.SetResult(result);
    }
}
