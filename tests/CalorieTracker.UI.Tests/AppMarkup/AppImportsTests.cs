namespace CalorieTracker.UI.Tests.AppMarkup;

/// <summary>Without the Web using in the App's _Imports.razor, an @onclick in an App page renders as plain text and never fires.</summary>
public class AppImportsTests
{
    [Test]
    public void AppImports_HaveTheWebUsing() =>
        Assert.That(RepoFiles.Read("src", "CalorieTracker.App", "Components", "_Imports.razor"), Does.Contain("@using Microsoft.AspNetCore.Components.Web"));
}
