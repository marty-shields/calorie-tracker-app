using CalorieTracker.UI.Components.Inputs;

namespace CalorieTracker.UI.Tests.Components.Inputs;

public class TextFieldTests : ComponentTestBase
{
    private IRenderedComponent<TextField> Render(string? value = "1 slice", Action<string>? changed = null, string? invalid = null, bool disabled = false) =>
        Context.Render<TextField>(p => p
            .Add(x => x.Label, "Serving name")
            .Add(x => x.Value, value)
            .Add(x => x.MaxLength, 60)
            .Add(x => x.Placeholder, "Serving name")
            .Add(x => x.InvalidMessage, invalid)
            .Add(x => x.Disabled, disabled)
            .Add(x => x.TestId, "name")
            .Add(x => x.ValueChanged, changed ?? (_ => { })));

    [Test]
    public void Renders_ATextInputWithItsValueTestIdMaxLengthPlaceholderAndAHiddenLabel()
    {
        var cut = Render();

        var input = cut.Find("input");
        Assert.Multiple(() =>
        {
            Assert.That((input.GetAttribute("type"), input.GetAttribute("value"), input.GetAttribute("data-testid")), Is.EqualTo(("text", "1 slice", "name")));
            Assert.That((input.GetAttribute("maxlength"), input.GetAttribute("placeholder")), Is.EqualTo(("60", "Serving name")));
            Assert.That(cut.Find(".visually-hidden").TextContent, Is.EqualTo("Serving name"));
        });
    }

    [Test]
    public void Typing_RaisesTheTextOnEveryKeystroke()
    {
        var typed = new List<string>();
        var cut = Render("", typed.Add);

        cut.Find("input").Input("A");
        cut.Find("input").Input("A slice");

        Assert.That(typed, Is.EqualTo(new[] { "A", "A slice" }));
    }

    [Test]
    public void InvalidMessage_ShowsAnAnnouncedErrorLinkedToTheInput()
    {
        var cut = Render(invalid: "Too long");

        var message = cut.Find("[data-testid=name-error]");
        Assert.Multiple(() =>
        {
            Assert.That(message.TextContent, Is.EqualTo("Too long"));
            Assert.That(message.GetAttribute("role"), Is.EqualTo("alert"));
            Assert.That(cut.Find("input").GetAttribute("aria-invalid"), Is.EqualTo("true"));
            Assert.That(cut.Find("input").GetAttribute("aria-describedby"), Is.EqualTo(message.Id));
        });
    }

    [Test]
    public void NoInvalidMessage_ShowsNoError() =>
        Assert.That(Render().FindAll("[role=alert]"), Is.Empty);

    [Test]
    public void Disabled_DisablesTheInput() =>
        Assert.That(Render(disabled: true).Find("input").HasAttribute("disabled"), Is.True);
}
