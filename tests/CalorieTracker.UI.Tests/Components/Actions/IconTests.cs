using CalorieTracker.UI.Components.Actions;

namespace CalorieTracker.UI.Tests.Components.Actions;

public class IconTests : ComponentTestBase
{
    [Test]
    public void Icon_IsHiddenFromScreenReaders()
    {
        var cut = Context.Render<Icon>(p => p.Add(x => x.Name, IconName.Chevron));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("svg").GetAttribute("aria-hidden"), Is.EqualTo("true"));
            Assert.That(cut.Find("svg").GetAttribute("focusable"), Is.EqualTo("false"));
        });
    }

    [Test]
    public void EachIcon_DrawsItsOwnPath()
    {
        var paths = Enum.GetValues<IconName>()
            .Select(name => Context.Render<Icon>(p => p.Add(x => x.Name, name)).Find("path").GetAttribute("d"))
            .ToList();

        Assert.That(paths, Is.Unique.And.All.Not.Empty);
    }

    [Test]
    public void Default_IsNeitherMutedNorSmall()
    {
        var cut = Context.Render<Icon>(p => p.Add(x => x.Name, IconName.Chevron));

        Assert.That(cut.Find("svg").ClassList, Is.EquivalentTo(new[] { "icon" }));
    }

    [TestCase(IconTone.Muted, "muted")]
    [TestCase(IconTone.Success, "success")]
    [TestCase(IconTone.Error, "error")]
    public void Tone_SetsClass(IconTone tone, string expected)
    {
        var cut = Context.Render<Icon>(p => p.Add(x => x.Name, IconName.Up).Add(x => x.Tone, tone));

        Assert.That(cut.Find("svg").ClassList, Is.EquivalentTo(new[] { "icon", expected }));
    }

    [Test]
    public void SmallSize_SetsClass()
    {
        var cut = Context.Render<Icon>(p => p.Add(x => x.Name, IconName.Chevron).Add(x => x.Size, IconSize.Small));

        Assert.That(cut.Find("svg").ClassList, Does.Contain("small"));
    }

    [Test]
    public void NewIcons_RenderInEveryToneAndSize(
        [Values(IconName.Plus, IconName.Minus, IconName.ChevronLeft)] IconName name,
        [Values] IconTone tone,
        [Values] IconSize size)
    {
        var cut = Context.Render<Icon>(p => p.Add(x => x.Name, name).Add(x => x.Tone, tone).Add(x => x.Size, size));

        var expected = new List<string> { "icon" };
        if (tone != IconTone.Default)
        {
            expected.Add(tone.ToString().ToLowerInvariant());
        }

        if (size == IconSize.Small)
        {
            expected.Add("small");
        }

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("svg").ClassList, Is.EquivalentTo(expected));
            Assert.That(cut.Find("path").GetAttribute("d"), Is.Not.Empty);
        });
    }
}
