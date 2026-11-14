using CalorieTracker.UI.Components.Lists;

namespace CalorieTracker.UI.Tests.Components.Lists;

public class ListRowTests : ComponentTestBase
{
    [Test]
    public void Render_ShowsTextDetailAndAside()
    {
        var cut = Context.Render<ListRow>(p => p
            .Add(x => x.Text, "Porridge oats")
            .Add(x => x.Detail, "Quaker")
            .Add(x => x.Aside, "372 kcal / 100 g"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find(".text").TextContent, Is.EqualTo("Porridge oats"));
            Assert.That(cut.Find(".detail").TextContent, Is.EqualTo("Quaker"));
            Assert.That(cut.Find(".aside").TextContent, Is.EqualTo("372 kcal / 100 g"));
        });
    }

    [Test]
    public void Render_WithoutOnClick_IsNotAButtonAndHasNoChevron()
    {
        var cut = Context.Render<ListRow>(p => p.Add(x => x.Text, "Porridge oats"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("button"), Is.Empty);
            Assert.That(cut.FindAll(".chevron"), Is.Empty);
        });
    }

    [Test]
    public void Click_RaisesOnClick()
    {
        var clicked = false;
        var cut = Context.Render<ListRow>(p => p
            .Add(x => x.Text, "Porridge oats")
            .Add(x => x.OnClick, () => clicked = true)
            .Add(x => x.TestId, "row"));

        cut.Find("[data-testid=row]").Click();

        Assert.That(clicked, Is.True);
    }
}
