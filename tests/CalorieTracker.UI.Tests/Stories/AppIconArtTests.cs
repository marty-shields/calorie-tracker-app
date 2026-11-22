using System.Text;
using CalorieTracker.UI.Stories.Stories.Foundations;

namespace CalorieTracker.UI.Tests.Stories;

public class AppIconArtTests
{
    private const string DataUri = "data:image/svg+xml;base64,";

    [Test]
    public void Art_IsTheAppsIconAndSplash() =>
        Assert.Multiple(() =>
        {
            Assert.That(Decode(AppIconArt.Background), Is.EqualTo(App("AppIcon", "appicon.svg")));
            Assert.That(Decode(AppIconArt.Foreground), Is.EqualTo(App("AppIcon", "appiconfg.svg")));
            Assert.That(Decode(AppIconArt.Monochrome), Is.EqualTo(App("AppIcon", "appicon_monochrome.svg")));
            Assert.That(Decode(AppIconArt.SplashLight), Is.EqualTo(App("Splash", "splash.svg")));
            Assert.That(Decode(AppIconArt.SplashDark), Is.EqualTo(App("AppIcon", "appiconfg.svg")), "the dark splash shows the icon's foreground");
        });

    private static string App(string folder, string file) => RepoFiles.Read("src", "CalorieTracker.App", "Resources", folder, file);

    private static string Decode(string src) => Encoding.UTF8.GetString(Convert.FromBase64String(src[DataUri.Length..]));
}
