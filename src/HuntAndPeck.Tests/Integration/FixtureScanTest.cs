using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using UIAutomationClient;
using Xunit;

namespace HuntAndPeck.Tests.Integration
{
    /// <summary>
    /// Scans the WinForms fixture window through the app's real UI Automation code (scanner, hint factory)
    /// </summary>
    /// <remarks>
    /// The fixture's controls do not overlap, so a control's hint is the hint whose bounds match the control's
    /// own UIA bounds. Controls are found by Name (buttons, check box, labels, list items) or ControlType
    /// (combo box and text box have an empty Name).
    /// </remarks>
    [Trait("Category", "Integration")]
    [Collection(UiaFixtureApp.CollectionName)]
    public class FixtureScanTest : IClassFixture<UiaFixtureApp>
    {
        private const double BoundsTolerance = 1;
        private readonly UiaFixtureApp _app;

        public FixtureScanTest(UiaFixtureApp app)
        {
            _app = app;
        }

        [Fact]
        public async Task TryScan_FindsElementsOfFixtureWindow()
        {
            var scan = await FixtureUia.TryScanAsync(_app);

            Assert.NotNull(scan);
            Assert.NotEmpty(scan.Elements);
        }

        [Fact]
        public async Task Button_GetsInvokeHint()
        {
            var hint = await SingleHintAtAsync(UIA_PropertyIds.UIA_NamePropertyId, "Press me");

            Assert.IsType<UiAutomationInvokeHint>(hint);
        }

        /// <remarks>
        /// A WinForms check box supports both Invoke and Toggle; the selector prefers Invoke (see
        /// UiAutomationHintKindSelector), so the hint is an invoke hint, not a toggle hint
        /// </remarks>
        [Fact]
        public async Task CheckBox_SupportsToggle_ButGetsInvokeHint()
        {
            Assert.True(await FixtureUia.SupportsPatternAsync(
                _app, UIA_PropertyIds.UIA_NamePropertyId, "Check me", UIA_PropertyIds.UIA_IsTogglePatternAvailablePropertyId));

            var hint = await SingleHintAtAsync(UIA_PropertyIds.UIA_NamePropertyId, "Check me");

            Assert.IsType<UiAutomationInvokeHint>(hint);
        }

        [Fact]
        public async Task ComboBox_GetsExpandCollapseHint()
        {
            var hint = await SingleHintAtAsync(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_ComboBoxControlTypeId);

            Assert.IsType<UiAutomationExpandCollapseHint>(hint);
        }

        /// <remarks>
        /// A WinForms list item supports both Invoke and SelectionItem; the selector prefers Invoke, so each
        /// item gets an invoke hint, not a select hint
        /// </remarks>
        [Fact]
        public async Task ListItems_SupportSelectionItem_ButGetInvokeHints()
        {
            foreach (var name in new[] { "List one", "List two", "List three" })
            {
                Assert.True(await FixtureUia.SupportsPatternAsync(
                    _app, UIA_PropertyIds.UIA_NamePropertyId, name, UIA_PropertyIds.UIA_IsSelectionItemPatternAvailablePropertyId));

                var hint = await SingleHintAtAsync(UIA_PropertyIds.UIA_NamePropertyId, name);

                Assert.IsType<UiAutomationInvokeHint>(hint);
            }
        }

        [Fact]
        public async Task TextBox_GetsFocusHint()
        {
            var hint = await SingleHintAtAsync(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId);

            Assert.IsType<UiAutomationFocusHint>(hint);
        }

        [Fact]
        public async Task StaticLabel_GetsNoHint()
        {
            var labelBounds = await FindSingleBoundsAsync(UIA_PropertyIds.UIA_NamePropertyId, "Static text");
            var hints = await FixtureUia.ScanHintsAsync(_app);

            Assert.DoesNotContain(hints, x => x.BoundingRectangle.IntersectsWith(labelBounds));
        }

        private async Task<Hint> SingleHintAtAsync(int propertyId, object value)
        {
            var bounds = await FindSingleBoundsAsync(propertyId, value);
            var hints = await FixtureUia.ScanHintsAsync(_app);
            return Assert.Single(hints, x => IsSameBounds(x.BoundingRectangle, bounds));
        }

        private async Task<Rect> FindSingleBoundsAsync(int propertyId, object value)
        {
            List<Rect> found = await FixtureUia.FindBoundsAsync(_app, propertyId, value);
            return found.First();
        }

        private static bool IsSameBounds(Rect a, Rect b)
        {
            return System.Math.Abs(a.Left - b.Left) <= BoundsTolerance
                && System.Math.Abs(a.Top - b.Top) <= BoundsTolerance
                && System.Math.Abs(a.Right - b.Right) <= BoundsTolerance
                && System.Math.Abs(a.Bottom - b.Bottom) <= BoundsTolerance;
        }
    }
}
