using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

public class OnlineSearchNoticeTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeLogger<OnlineSearchNotice> logger = null!;
    private OnlineSearchNotice notice = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<AppSettings>();
        logger = new FakeLogger<OnlineSearchNotice>();
        notice = new OnlineSearchNotice(new NoticeStore(connection, new DatabaseGate()), logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task Fresh_IsNotShown_UntilMarked_ThenStaysShown()
    {
        var before = await notice.WasShownAsync();
        await notice.MarkShownAsync();
        await notice.MarkShownAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(before, Is.False);
            Assert.That(await notice.WasShownAsync());
            Assert.That(await connection.Table<AppSettings>().CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Marking_KeepsTheDailyTarget_AndTheTargetKeepsTheMark()
    {
        await connection.InsertAsync(new AppSettings { DailyTargetKcal = 2100 });
        await notice.MarkShownAsync();
        var targets = new TargetStore(connection, new DatabaseGate());
        await connection.CreateTableAsync<TargetHistory>();
        await targets.SetAsync(1800, new DateOnly(2031, 7, 19));

        Assert.Multiple(async () =>
        {
            Assert.That(await targets.GetAsync(), Is.EqualTo(1800));
            Assert.That(await notice.WasShownAsync());
        });
    }

    [Test]
    public async Task AColumnAddedToAnOlderSettingsTable_IsFilledInByTheTableCreation_AndKeepsTheTarget()
    {
        await connection.ExecuteAsync("DROP TABLE AppSettings");
        await connection.ExecuteAsync("CREATE TABLE AppSettings (Id integer not null primary key, DailyTargetKcal integer)");
        await connection.ExecuteAsync("INSERT INTO AppSettings (Id, DailyTargetKcal) VALUES (1, 2100)");

        await connection.CreateTableAsync<AppSettings>();

        Assert.Multiple(async () =>
        {
            Assert.That(await new TargetStore(connection, new DatabaseGate()).GetAsync(), Is.EqualTo(2100));
            Assert.That(await notice.WasShownAsync(), Is.False);
        });
    }

    [Test]
    public async Task AFailingDatabase_NeverThrows_ShowsTheNoticeAgain_AndLogsNothingElse()
    {
        await connection.ExecuteAsync("DROP TABLE AppSettings");

        var shown = await notice.WasShownAsync();
        await notice.MarkShownAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.False);
            Assert.That(LoggedEvents.Of(logger).Select(e => (e.Id, e.Message)), Is.EqualTo(new[]
            {
                (CoreEvent.OnlineNoticeReadFailed, "Reading whether the search notice was shown failed"),
                (CoreEvent.OnlineNoticeSaveFailed, "Saving that the search notice was shown failed"),
            }));
        });
    }

    [Test]
    public void TheText_IsTheOwnersLine() =>
        Assert.That(OnlineSearchNotice.Text, Is.EqualTo("This sends what you typed to an online database."));
}
