namespace CalorieTracker.UI.Tests.Lifecycle;

/// <summary>
/// The App passes the activity's lifecycle to <c>AndroidBackButton</c> (with the kit's <c>BackButtonBridge</c>), the kit's <c>AppLifecycleBridge</c>, <c>AndroidKeyboardRestore</c> and <c>AndroidDiagnostics</c>;
/// the back button, app start and keyboard restore follow only <c>MainActivity</c>, not MAUI's own activities, except that destroy is passed on and each class ignores any activity but its own.
/// The kit tests the bridges' logic and registers them (<c>AddShieldsUi</c>); only the Android plumbing is read here.
/// The kit's back button and app lifecycle are scoped to the BlazorWebView, so the kit's <c>HostBridges</c>, first in the App's <c>Routes</c> component and so inside that scope, connects them to the Android side; they are never resolved from the root provider.
/// The App can't be referenced, so its files are read. Whitespace is ignored.
/// </summary>
public class ActivityWiringTests
{
    [TestCase("MauiProgram.cs", "AddBackButton(builder);")]
    [TestCase("MauiProgram.cs", "if (activity is MainActivity main) { BackButton().Attach(main); }")]
    [TestCase("MauiProgram.cs", ".OnDestroy(activity => BackButton().Detach(activity))")]
    [TestCase("MauiProgram.cs", "AddAppLifecycle(builder);")]
    [TestCase("MauiProgram.cs", "if (activity is MainActivity) { Lifecycle().Attach(activity); }")]
    [TestCase("MauiProgram.cs", "if (activity is MainActivity) { _ = Lifecycle().StartedAsync(); }")]
    [TestCase("MauiProgram.cs", ".OnDestroy(activity => Lifecycle().Detach(activity))")]
    [TestCase("MauiProgram.cs", "AddDiagnostics(builder);")]
    [TestCase("MauiProgram.cs", ".OnStart(_ => Diagnostics().ActivityStarted())")]
    [TestCase("MauiProgram.cs", "AddKeyboardRestore(builder);")]
    [TestCase("MauiProgram.cs", "builder.Services.AddSingleton<AndroidKeyboardRestore>();")]
    [TestCase("MauiProgram.cs", ".OnPause(activity => { if (activity is MainActivity) { KeyboardRestore().Paused(activity); } })")]
    [TestCase("MauiProgram.cs", ".OnCreate((activity, _) => { if (activity is MainActivity) { KeyboardRestore().Attach(activity); } })")]
    [TestCase("MauiProgram.cs", ".OnResume(activity => { if (activity is MainActivity) { KeyboardRestore().Resumed(activity); } })")]
    [TestCase("MauiProgram.cs", ".OnDestroy(activity => KeyboardRestore().Detach(activity))")]
    [TestCase("Components/Routes.razor", "<HostBridges />")]
    [TestCase("Navigation/AndroidBackButton.cs", "using Shields.UI.Navigation;")]
    [TestCase("MauiProgram.cs", "using Shields.UI.Lifecycle;")]
    [TestCase("Navigation/AndroidBackButton.cs", "bridge.EnabledChanged += Update;")]
    [TestCase("Navigation/AndroidBackButton.cs", "bridge.Attach(activity);")]
    [TestCase("Navigation/AndroidBackButton.cs", "bridge.Detach(activity);")]
    [TestCase("Navigation/AndroidBackButton.cs", "callback.Enabled = bridge.Enabled;")]
    [TestCase("Navigation/AndroidBackButton.cs", "if (bridge.HandleAsync() is not null)")]
    [TestCase("MauiProgram.cs", "builder.Services.AddSingleton<AndroidBackButton>();")]
    [TestCase("Diagnostics/AndroidDiagnostics.cs", "public void ActivityStarted() => Log.ActivityStarted(logger);")]
    [TestCase("Diagnostics/AndroidDiagnostics.cs", "[LoggerMessage((int)AppEvent.ActivityStarted, LogLevel.Information, \"Activity started\")]")]
    public void App_PassesTheActivityLifecycle(string file, string code) =>
        Assert.That(WithoutWhitespace(RepoFiles.Read(["src", "CalorieTracker.App", .. file.Split('/')])), Does.Contain(WithoutWhitespace(code)));

    [TestCase("MauiProgram.cs", "GetRequiredService<IBackButton>")]
    [TestCase("MauiProgram.cs", "GetRequiredService<IAppLifecycle>")]
    [TestCase("MauiProgram.cs", "GetRequiredService<IAppStart>")]
    [TestCase("MauiProgram.cs", "AddSingleton<BackButtonBridge>")]
    [TestCase("MauiProgram.cs", "AddSingleton<AppLifecycleBridge>")]
    [TestCase("Components/Routes.razor", ".Connect(")]
    [TestCase("Components/Routes.razor", ".Disconnect(")]
    [TestCase("Components/Routes.razor", "@inject IBackButton")]
    [TestCase("Components/Routes.razor", "@inject IAppLifecycle")]
    [TestCase("MauiProgram.cs", "AddScoped<IBackButton")]
    [TestCase("MauiProgram.cs", "AddSingleton<IBackButton")]
    public void App_NeverResolvesOrRegistersTheScopedKitServicesFromTheRoot(string file, string code) =>
        Assert.That(WithoutWhitespace(RepoFiles.Read(["src", "CalorieTracker.App", .. file.Split('/')])), Does.Not.Contain(WithoutWhitespace(code)));

    [Test]
    public void Routes_PutsHostBridgesFirst() =>
        Assert.That(WithoutWhitespace(RepoFiles.Read("src", "CalorieTracker.App", "Components", "Routes.razor")), Does.Contain("@injectNavigationManagerNavigation<HostBridges/><SystemBarInsets/>"),
            "HostBridges clears the handlers registered earlier in the scope, so it comes before every other component");

    private static string WithoutWhitespace(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
