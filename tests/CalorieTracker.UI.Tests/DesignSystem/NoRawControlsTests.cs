using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>App markup (including a <c>&lt;style&gt;</c> block, which would restyle the kit) holds no raw control; screens and components use the kit's parts, so a control looks and behaves the same everywhere.</summary>
public partial class NoRawControlsTests
{
    private static readonly string[] AppFolders = ["src/CalorieTracker.UI", "src/CalorieTracker.App"];

    /// <summary>The lines of markup (1-based) that open a raw control element, as "line: &lt;element".</summary>
    internal static IReadOnlyList<string> RawControls(string markup) =>
        markup.Split('\n')
            .Select((text, index) => (Match: RawControlPattern().Match(text), Line: index + 1))
            .Where(item => item.Match.Success)
            .Select(item => $"{item.Line}: {item.Match.Value}")
            .ToList();

    [Test]
    public void AppMarkup_HasNoRawControl()
    {
        var found = AppFolders
            .SelectMany(folder => RepoFiles.Find("*.razor", folder.Split('/')).Concat(RepoFiles.Find("*.html", folder.Split('/'))))
            .SelectMany(file => RawControls(File.ReadAllText(file)).Select(control => $"{Path.GetRelativePath(RepoFiles.Root, file)}:{control}"))
            .ToList();
        Assert.That(found, Is.Empty, "use the kit's component, or request one in app-ui-kit");
    }

    [Test]
    public void RawControls_NamesTheLineOfEachRawElementButNotAKitComponent() =>
        Assert.That(
            RawControls("<AppButton>ok</AppButton>\n<div>\n  <button type=\"button\">x</button>\n<Input />\n<input type=\"search\" /><svg></svg>\n<TextField />\n<select><textarea></textarea><dialog>\n<style>.x{color:red}</style>"),
            Is.EqualTo(new[] { "3: <button", "5: <input", "7: <select", "8: <style" }));

    [GeneratedRegex("<(button|input|select|textarea|svg|dialog|style)\\b")]
    private static partial Regex RawControlPattern();
}
