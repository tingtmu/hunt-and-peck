using System.Linq;
using System.Threading.Tasks;
using HuntAndPeck.Models;
using UIAutomationClient;
using Xunit;

namespace HuntAndPeck.Tests.Integration
{
    /// <summary>
    /// Invokes a hint of the WinForms fixture window and checks the app reacted
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(UiaFixtureApp.CollectionName)]
    public class FixtureInvokeTest : IClassFixture<UiaFixtureApp>
    {
        private readonly UiaFixtureApp _app;

        public FixtureInvokeTest(UiaFixtureApp app)
        {
            _app = app;
        }

        [Fact]
        public async Task InvokingButtonHint_ChangesLabelToPressed()
        {
            Assert.True(await FixtureUia.WaitForNameAsync(_app, "Not pressed"));
            var buttonBounds = (await FixtureUia.FindBoundsAsync(_app, UIA_PropertyIds.UIA_NamePropertyId, "Press me")).First();
            var hints = await FixtureUia.ScanHintsAsync(_app);
            var hint = hints.OfType<UiAutomationInvokeHint>().Single(x => System.Math.Abs(x.BoundingRectangle.Left - buttonBounds.Left) <= 1 && System.Math.Abs(x.BoundingRectangle.Top - buttonBounds.Top) <= 1);

            await FixtureUia.InvokeAsync(_app, hint);

            Assert.True(await FixtureUia.WaitForNameAsync(_app, "Pressed"), "The label did not change to 'Pressed'");
        }
    }
}

