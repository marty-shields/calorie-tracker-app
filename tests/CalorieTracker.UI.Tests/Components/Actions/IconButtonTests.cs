using CalorieTracker.UI.Components.Actions;

namespace CalorieTracker.UI.Tests.Components.Actions;

public class IconButtonTests : ComponentTestBase
{
    [Test]
    public void Click_RaisesOnClick()
    {
        var clicked = false;
        var cut = Context.Render<IconButton>(p => p
            .Add(x => x.Icon, IconName.Settings)
            .Add(x => x.AriaLabel, "Settings")
            .Add(x => x.OnClick, () => clicked = true));

        cut.Find("button").Click();

        Assert.That(clicked, Is.True);
    }

    [Test]
    public async Task FocusAsync_FocusesTheButton()
    {
        var cut = Context.Render<IconButton>(p => p.Add(x => x.Icon, IconName.Delete).Add(x => x.AriaLabel, "Delete"));

        await cut.InvokeAsync(() => cut.Instance.FocusAsync());

        Context.JSInterop.VerifyFocusAsyncInvoke().Arguments[0].ShouldBeElementReferenceTo(cut.Find("button"));
    }

    [Test]
    public void Attributes_LabelTheButtonAndHideTheIcon()
    {
        var cut = Context.Render<IconButton>(p => p
            .Add(x => x.Icon, IconName.Back)
            .Add(x => x.AriaLabel, "Back")
            .Add(x => x.TestId, "back"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("button").GetAttribute("aria-label"), Is.EqualTo("Back"));
            Assert.That(cut.Find("button").GetAttribute("data-testid"), Is.EqualTo("back"));
            Assert.That(cut.Find("svg").GetAttribute("aria-hidden"), Is.EqualTo("true"));
        });
    }

    [Test]
    public void IconMutedAndSize_ArePassedToTheIcon()
    {
        var cut = Context.Render<IconButton>(p => p
            .Add(x => x.Icon, IconName.Delete)
            .Add(x => x.AriaLabel, "Delete")
            .Add(x => x.Muted, true)
            .Add(x => x.Size, IconSize.Small));
        var icon = Context.Render<Icon>(p => p.Add(x => x.Name, IconName.Delete));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("path").GetAttribute("d"), Is.EqualTo(icon.Find("path").GetAttribute("d")));
            Assert.That(cut.Find("svg").ClassList, Does.Contain("muted").And.Contain("small"));
            Assert.That(cut.Find("button").ClassList, Is.EquivalentTo(new[] { "icon-btn" }));
        });
    }

    [Test]
    public void Active_SetsClass()
    {
        var cut = Context.Render<IconButton>(p => p
            .Add(x => x.Icon, IconName.Edit)
            .Add(x => x.AriaLabel, "Edit")
            .Add(x => x.Active, true));

        Assert.That(cut.Find("button").ClassList, Is.EquivalentTo(new[] { "icon-btn", "active" }));
    }

    [Test]
    public void Compact_SetsClass()
    {
        var cut = Context.Render<IconButton>(p => p
            .Add(x => x.Icon, IconName.Edit)
            .Add(x => x.AriaLabel, "Edit set 1")
            .Add(x => x.Compact, true));

        Assert.That(cut.Find("button").ClassList, Is.EquivalentTo(new[] { "icon-btn", "compact" }));
    }

    [Test]
    public void NotCompact_HasNoCompactClass()
    {
        var cut = Context.Render<IconButton>(p => p
            .Add(x => x.Icon, IconName.Edit)
            .Add(x => x.AriaLabel, "Edit"));

        Assert.That(cut.Find("button").ClassList, Does.Not.Contain("compact"));
    }

    [Test]
    public void Filled_SetsClass()
    {
        var cut = Context.Render<IconButton>(p => p
            .Add(x => x.Icon, IconName.Plus)
            .Add(x => x.AriaLabel, "Add to Lunch")
            .Add(x => x.TestId, "add")
            .Add(x => x.Style, IconButtonStyle.Filled));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("button").ClassList, Is.EquivalentTo(new[] { "icon-btn", "filled" }));
            Assert.That(cut.Find("button").GetAttribute("aria-label"), Is.EqualTo("Add to Lunch"));
            Assert.That(cut.Find("button").GetAttribute("data-testid"), Is.EqualTo("add"));
        });
    }

    [Test]
    public void Filled_IgnoresMutedOnTheIcon()
    {
        var cut = Context.Render<IconButton>(p => p
            .Add(x => x.Icon, IconName.Plus)
            .Add(x => x.AriaLabel, "Add")
            .Add(x => x.Muted, true)
            .Add(x => x.Style, IconButtonStyle.Filled));

        Assert.That(cut.Find("svg").ClassList, Does.Not.Contain("muted"));
    }

    [Test]
    public void Plain_IsTheDefaultAndHasNoFilledClass()
    {
        var cut = Context.Render<IconButton>(p => p.Add(x => x.Icon, IconName.Plus).Add(x => x.AriaLabel, "Add"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Instance.Style, Is.EqualTo(IconButtonStyle.Plain));
            Assert.That(cut.Find("button").ClassList, Is.EquivalentTo(new[] { "icon-btn" }));
        });
    }

    [Test]
    public void FilledChip_KeepsTheTouchTargetAndFillsFromTokens()
    {
        var root = RepoFiles.Find("*.razor", "src", "CalorieTracker.UI").Single(f => f.EndsWith("IconButton.razor"));
        var css = File.ReadAllText(root + ".css");
        var baseRule = css[..css.IndexOf(".compact", StringComparison.Ordinal)];

        Assert.Multiple(() =>
        {
            Assert.That(baseRule, Does.Contain("min-width: var(--touch-target)").And.Contain("min-height: var(--touch-target)"));
            Assert.That(css, Does.Contain(".filled").And.Contain("background-color: var(--color-chip)").And.Contain("color: var(--color-on-chip)"));
        });
    }
}
