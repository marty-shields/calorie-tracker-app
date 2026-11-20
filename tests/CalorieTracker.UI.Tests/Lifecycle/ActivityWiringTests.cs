namespace CalorieTracker.UI.Tests.Lifecycle;

/// <summary>
/// The App passes the activity's lifecycle to <c>AndroidBackButton</c> (with the UI's <c>BackButtonBridge</c>), the UI's <c>AppLifecycleBridge</c>, <c>AndroidKeyboardRestore</c> and <c>AndroidDiagnostics</c>;
/// the back button, app start and keyboard restore follow only <c>MainActivity</c>, not MAUI's own activities, except that destroy is passed on and each class ignores any activity but its own.
/// The bridges' logic is tested directly (<c>BackButtonBridgeTests</c>, <c>AppLifecycleBridgeTests</c>); only the Android plumbing is read here.
/// The kit's back button and app lifecycle are scoped to the BlazorWebView, so the App's <c>Routes</c> component, which runs inside that scope, connects them to the Android side; they are never resolved from the root provider.
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
    [TestCase("MauiProgram.cs", "builder.Services.AddSingleton<AppLifecycleBridge>();")]
    [TestCase("MauiProgram.cs", "builder.Services.AddSingleton<BackButtonBridge>();")]
    [TestCase("Navigation/AndroidBackButton.cs", "bridge.EnabledChanged += Update;")]
    [TestCase("Navigation/AndroidBackButton.cs", "bridge.Attach(activity);")]
    [TestCase("Navigation/AndroidBackButton.cs", "bridge.Detach(activity);")]
    [TestCase("Navigation/AndroidBackButton.cs", "callback.Enabled = bridge.Enabled;")]
    [TestCase("Navigation/AndroidBackButton.cs", "if (bridge.HandleAsync() is not null)")]
    [TestCase("MauiProgram.cs", "builder.Services.AddSingleton<AndroidBackButton>();")]
    [TestCase("Components/Routes.razor", "@inject IBackButton BackButton")]
    [TestCase("Components/Routes.razor", "@inject BackButtonBridge BackBridge")]
    [TestCase("Components/Routes.razor", "@inject AppLifecycleBridge LifecycleBridge")]
    [TestCase("Components/Routes.razor", "@inject IAppLifecycle AppLifecycle")]
    [TestCase("Components/Routes.razor", "BackBridge.Connect(BackButton);")]
    [TestCase("Components/Routes.razor", "LifecycleBridge.Connect(AppLifecycle);")]
    [TestCase("Components/Routes.razor", "BackBridge.Disconnect(BackButton);")]
    [TestCase("Components/Routes.razor", "LifecycleBridge.Disconnect(AppLifecycle);")]
    [TestCase("Diagnostics/AndroidDiagnostics.cs", "public void ActivityStarted() => Log.ActivityStarted(logger);")]
    [TestCase("Diagnostics/AndroidDiagnostics.cs", "[LoggerMessage((int)AppEvent.ActivityStarted, LogLevel.Information, \"Activity started\")]")]
    public void App_PassesTheActivityLifecycle(string file, string code) =>
        Assert.That(WithoutWhitespace(RepoFiles.Read(["src", "CalorieTracker.App", .. file.Split('/')])), Does.Contain(WithoutWhitespace(code)));

    [TestCase("MauiProgram.cs", "GetRequiredService<IBackButton>")]
    [TestCase("MauiProgram.cs", "GetRequiredService<IAppLifecycle>")]
    [TestCase("MauiProgram.cs", "GetRequiredService<IAppStart>")]
    [TestCase("MauiProgram.cs", "AddScoped<IBackButton")]
    [TestCase("MauiProgram.cs", "AddSingleton<IBackButton")]
    public void App_NeverResolvesOrRegistersTheScopedKitServicesFromTheRoot(string file, string code) =>
        Assert.That(WithoutWhitespace(RepoFiles.Read(["src", "CalorieTracker.App", .. file.Split('/')])), Does.Not.Contain(WithoutWhitespace(code)));

    private static string WithoutWhitespace(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
