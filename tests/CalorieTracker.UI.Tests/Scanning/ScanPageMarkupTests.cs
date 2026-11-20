using System.Text.RegularExpressions;
using CalorieTracker.UI.Tests.DesignSystem;
using Shields.UI.Theme;

namespace CalorieTracker.UI.Tests.Scanning;

/// <summary>The App can't be referenced, so its scan page markup is read.</summary>
public class ScanPageMarkupTests
{
    [TestCase("Ean13")]
    [TestCase("Ean8")]
    [TestCase("Upca")]
    public void ScanPage_ListsTheSymbology(string symbology) =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml"), Does.Match(@"BarcodeSymbologies=""[^""]*\b" + symbology + @"\b"));

    [Test]
    public void ScanPage_ShowsTheHintOverTheCamera() =>
        Assert.That(ScanXaml(), Does.Match(@"<Label[^>]*AutomationId=""scan-native-hint""[^>]*Text=""Point the camera at a barcode"""));

    [TestCase("scan:CameraView")]
    [TestCase("Border")]
    [TestCase("Label")]
    public void ScanPage_PutsTheCameraFrameAndHintInTheSameGridCell(string element)
    {
        var tag = Regex.Match(ScanXaml(), "<" + element + @"\s[^>]*>").Value;
        Assert.That(tag, Is.Not.Empty);
        Assert.That(tag, Does.Not.Match(@"Grid\.Row=""[1-9]"""));
        Assert.That(tag, Does.Not.Match(@"Grid\.Column="));
    }

    [Test]
    public void ScanPage_KeepsACancelButtonInTheBottomRowWiredToCancel() =>
        Assert.That(ScanXaml(), Does.Match(@"<Grid Grid\.Row=""1""[\s\S]*<Button AutomationId=""scan-native-cancel""[^>]*Text=""Cancel""[^>]*Clicked=""OnCancelClicked"""));

    [Test]
    public void ScanPage_OffersSearchByNameAboveCancel_WiredToItsOwnHandlerAndClosingWithTheSearchStatus()
    {
        var xaml = ScanXaml();
        var code = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml.cs");
        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Match(@"<Button AutomationId=""scan-native-search""[^>]*Text=""Search by name instead""[^>]*Clicked=""OnSearchByNameClicked"""));
            Assert.That(xaml.IndexOf("scan-native-search", StringComparison.Ordinal), Is.LessThan(xaml.IndexOf("scan-native-cancel", StringComparison.Ordinal)));
            Assert.That(code, Does.Contain("OnSearchByNameClicked(object? sender, EventArgs e) => await CloseAsync(ScanResult.SearchByName)"));
        });
    }

    [Test]
    public void ScanPage_ShowsTheCaptionPillCentredAtTheTop_AndFillsItFromTheScannerCaption()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ScanXaml(), Does.Match(@"<Label x:Name=""Caption""[^>]*AutomationId=""scan-native-caption""[^>]*HorizontalOptions=""Center""[^>]*VerticalOptions=""Start"""));
            Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml.cs"), Does.Contain("Caption.Text = caption;"));
        });
    }

    [Test]
    public void ScanPage_DrawsTheCaptionOnAWhitePillWithDarkText_FromThePageResourcesNotLiterals()
    {
        var xaml = ScanXaml();
        var pill = Regex.Match(xaml, @"<Border\s[^>]*AutomationId=""scan-native-caption-pill""[^>]*>").Value;
        Assert.Multiple(() =>
        {
            Assert.That(pill, Does.Contain("BackgroundColor=\"{StaticResource OverlayText}\"").And.Contain("StrokeShape=\"RoundRectangle"));
            Assert.That(xaml, Does.Match(@"<Label x:Name=""Caption""[^>]*TextColor=""\{StaticResource TextLight\}"""));
            Assert.That(xaml, Does.Not.Match(@"<Label x:Name=""Caption""[^>]*BackgroundColor"), "the label has no scrim of its own");
            Assert.That(AppColour("OverlayText"), Is.EqualTo("#ffffff"));
            Assert.That(AppColour("TextLight"), Is.EqualTo("#373e40"), "the light theme's --color-text");
        });
        Assert.That(TokensCss(), Does.Contain("--color-text: light-dark(#373e40,"));
    }

    [Test]
    public void ScanPage_HidesTheDecorativeFrameFromScreenReaders() =>
        Assert.That(ScanXaml(), Does.Match(@"<Border AutomationId=""scan-native-frame""\s+AutomationProperties\.IsInAccessibleTree=""False"""));

    [Test]
    public void ScanPage_DrawsTheFrameTwiceAsWideAsTall()
    {
        var tag = Regex.Match(ScanXaml(), @"<Border\s[^>]*AutomationId=""scan-native-frame""[^>]*>").Value;
        var width = double.Parse(Regex.Match(tag, @"WidthRequest=""([0-9.]+)""").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var height = double.Parse(Regex.Match(tag, @"HeightRequest=""([0-9.]+)""").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.That(width / height, Is.EqualTo(2.0));
    }

    [Test]
    public void ScanPage_UsesNoLiteralColoursInTheMarkup() =>
        Assert.That(ScanXaml(), Does.Not.Match(@"=""#[0-9A-Fa-f]").And.Not.Match(@"(Color|Stroke)=""(?!Transparent"")[A-Za-z]+"""));

    [TestCase("Background", "--color-bg")]
    [TestCase("Text", "--color-text")]
    [TestCase("BorderStrong", "--color-border-strong")]
    public void AppColours_MatchTheLightAndDarkToken(string key, string token)
    {
        var (light, dark) = DesignTokens.Colour(token);
        Assert.Multiple(() =>
        {
            Assert.That(AppColour(key + "Light"), Is.EqualTo(light.ToString()));
            Assert.That(AppColour(key + "Dark"), Is.EqualTo(dark.ToString()));
            Assert.That(light, Is.Not.EqualTo(dark), "a token with one value for both would hide a swapped pair");
        });
    }

    [Test]
    public void AppScrim_IsTheTokenAlphaInEachTheme()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AppColour("ScrimLight"), Is.EqualTo("#73000000"), "0.45 alpha black, as --color-scrim in light");
            Assert.That(AppColour("ScrimDark"), Is.EqualTo("#99000000"), "0.6 alpha black, as --color-scrim in dark");
        });
        Assert.That(TokensCss(), Does.Contain("--color-scrim: light-dark(rgb(0 0 0 / 0.45), rgb(0 0 0 / 0.6));"));
    }

    [Test]
    public void OverlayText_IsWhiteSoItShowsOverAnyCameraImage() =>
        Assert.That(AppColour("OverlayText"), Is.EqualTo("#ffffff"));

    [TestCase("Background")]
    [TestCase("Text")]
    [TestCase("BorderStrong")]
    [TestCase("Scrim")]
    public void ScanPage_PicksEachThemedColourWithAppThemeBinding(string key) =>
        Assert.That(ScanXaml(), Does.Contain("{AppThemeBinding Light={StaticResource " + key + "Light}, Dark={StaticResource " + key + "Dark}}"));

    [Test]
    public void ScanPage_DrawsTheFrameAndHintTextInTheOverlayColourAndTheBarInTheThemedOnes()
    {
        var xaml = ScanXaml();
        Assert.Multiple(() =>
        {
            Assert.That(xaml, Does.Match(@"Stroke=""\{StaticResource OverlayText\}"""));
            Assert.That(xaml, Does.Match(@"<Label[^>]*TextColor=""\{StaticResource OverlayText\}""[^>]*BackgroundColor=""\{AppThemeBinding Light=\{StaticResource ScrimLight\}"));
            Assert.That(xaml, Does.Match(@"<Grid Grid\.Row=""1""[^>]*BackgroundColor=""\{AppThemeBinding Light=\{StaticResource BackgroundLight\}"));
            Assert.That(xaml, Does.Match(@"<Button[^>]*BorderColor=""\{AppThemeBinding Light=\{StaticResource BorderStrongLight\}[^>]*TextColor=""\{AppThemeBinding Light=\{StaticResource TextLight\}"));
        });
    }

    [TestCase(ThemePreference.Light, "AppTheme.Light")]
    [TestCase(ThemePreference.Dark, "AppTheme.Dark")]
    [TestCase(ThemePreference.System, "AppTheme.Unspecified")]
    public void Scanner_BridgesTheAppThemeToUserAppTheme(ThemePreference preference, string appTheme)
    {
        var source = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "CameraBarcodeScanner.cs");
        var arm = preference == ThemePreference.System ? "_" : "ThemePreference." + preference;
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("app.UserAppTheme = theme switch"));
            Assert.That(source, Does.Contain(arm + " => " + appTheme + ","));
            Assert.That(source.IndexOf("UserAppTheme", StringComparison.Ordinal), Is.LessThan(source.IndexOf("new ScanPage(accept, caption)", StringComparison.Ordinal)), "set before the page is built");
        });
    }

    [Test]
    public void Scanner_RestoresThePreviousUserAppThemeInAFinallyAfterTheScan()
    {
        var source = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "CameraBarcodeScanner.cs");
        var finallyAt = source.IndexOf("finally", source.IndexOf("Task<ScanResult> OpenAsync(", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("var previousTheme = app?.UserAppTheme ?? AppTheme.Unspecified;"));
            Assert.That(finallyAt, Is.GreaterThan(source.IndexOf("await page.Result", StringComparison.Ordinal)), "restored after the page closes, on every path");
            Assert.That(source.IndexOf("app.UserAppTheme = previousTheme;", StringComparison.Ordinal), Is.GreaterThan(finallyAt));
        });
    }

    [Test]
    public void ScanPage_HasTextOnTheCancelButton() =>
        Assert.That(ScanXaml(), Does.Match(@"<Button AutomationId=""scan-native-cancel""[^>]*Text=""Cancel"""));

    [Test]
    public void ScanPage_OffersEachDetectionToTheCallerInsteadOfClosingOnTheFirst() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml.cs"), Does.Contain("ScanResult.FromRawValues(e.BarcodeResults.Select(b => b.RawValue), accept)"));

    [Test]
    public void ScanPage_TakesOnlyTheFirstOutcome() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml.cs"), Does.Match(@"if \(!completion\.TrySetResult\(result\)\)\s*\{\s*return;"));

    [Test]
    public void Scanner_PassesTheCallerAcceptToTheModal() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "CameraBarcodeScanner.cs"), Does.Contain("new ScanPage(accept, caption)"));

    [Test]
    public void Manifest_DeclaresTheCameraPermission() =>
        Assert.That(Manifest(), Does.Match(@"<uses-permission\s+android:name=""android\.permission\.CAMERA"""));

    [Test]
    public void Manifest_DeclaresTheCameraAsOptional() =>
        Assert.That(Manifest(), Does.Match(@"<uses-feature\s+android:name=""android\.hardware\.camera""\s+android:required=""false"""));

    private static string AppColour(string key) =>
        Regex.Match(RepoFiles.Read("src", "CalorieTracker.App", "App.xaml"), @"<Color x:Key=""" + key + @""">([^<]+)</Color>").Groups[1].Value;

    private static string TokensCss() => DesignTokens.KitTokensCss;

    private static string ScanXaml() => RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "ScanPage.xaml");

    private static string Manifest() => RepoFiles.Read("src", "CalorieTracker.App", "Platforms", "Android", "AndroidManifest.xml");
}
