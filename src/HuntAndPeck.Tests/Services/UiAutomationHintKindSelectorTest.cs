using HuntAndPeck.Services;
using Xunit;
using C = HuntAndPeck.Services.UiAutomationCapabilities;
using K = HuntAndPeck.Services.UiAutomationHintKind;

namespace HuntAndPeck.Tests.Services
{
    public class UiAutomationHintKindSelectorTest
    {
        private const int AllFlagCombinations = 1 << 9;

        [Theory]
        [InlineData(C.None, K.None)]
        [InlineData(C.InvokeAvailable, K.Invoke)]
        [InlineData(C.ToggleAvailable, K.Toggle)]
        [InlineData(C.SelectionItemAvailable, K.Select)]
        [InlineData(C.ExpandCollapseAvailable, K.ExpandCollapse)]
        [InlineData(C.ValueAvailable, K.Focus)]
        [InlineData(C.RangeValueAvailable, K.Focus)]
        public void SinglePattern(C capabilities, K expected)
        {
            Assert.Equal(expected, UiAutomationHintKindSelector.Select(capabilities));
        }

        [Theory]
        [InlineData(C.InvokeAvailable | C.ToggleAvailable | C.SelectionItemAvailable | C.ExpandCollapseAvailable | C.ValueAvailable, K.Invoke)]
        [InlineData(C.ToggleAvailable | C.SelectionItemAvailable | C.ExpandCollapseAvailable | C.ValueAvailable, K.Toggle)]
        [InlineData(C.SelectionItemAvailable | C.ExpandCollapseAvailable | C.RangeValueAvailable, K.Select)]
        [InlineData(C.ExpandCollapseAvailable | C.ValueAvailable | C.RangeValueAvailable, K.ExpandCollapse)]
        public void Precedence(C capabilities, K expected)
        {
            Assert.Equal(expected, UiAutomationHintKindSelector.Select(capabilities));
        }

        [Theory]
        [InlineData(C.ValueAvailable | C.ValueReadOnly, K.None)]
        [InlineData(C.RangeValueAvailable | C.RangeValueReadOnly, K.None)]
        [InlineData(C.ValueAvailable | C.ValueReadOnly | C.RangeValueAvailable, K.Focus)]
        [InlineData(C.ValueAvailable | C.RangeValueAvailable | C.RangeValueReadOnly, K.Focus)]
        [InlineData(C.ValueAvailable | C.ValueReadOnly | C.RangeValueAvailable | C.RangeValueReadOnly, K.None)]
        [InlineData(C.ValueReadOnly | C.RangeValueReadOnly, K.None)]
        [InlineData(C.InvokeAvailable | C.ValueReadOnly, K.Invoke)]
        public void FocusNeedsWritableValueOrRangeValue(C capabilities, K expected)
        {
            Assert.Equal(expected, UiAutomationHintKindSelector.Select(capabilities));
        }

        [Theory]
        [InlineData(C.LegacyDefaultActionAvailable, K.LegacyDefaultAction)]
        [InlineData(C.LegacyDefaultActionAvailable | C.ValueAvailable | C.ValueReadOnly, K.LegacyDefaultAction)]
        [InlineData(C.LegacyDefaultActionAvailable | C.InvokeAvailable, K.Invoke)]
        [InlineData(C.LegacyDefaultActionAvailable | C.ToggleAvailable, K.Toggle)]
        [InlineData(C.LegacyDefaultActionAvailable | C.ValueAvailable, K.Focus)]
        [InlineData(C.None, K.None)]
        public void LegacyDefaultAction_IsLastResort_WhenAllowed(C capabilities, K expected)
        {
            Assert.Equal(expected, UiAutomationHintKindSelector.Select(capabilities, true));
        }

        [Theory]
        [InlineData(50000, true)]   // Button
        [InlineData(50007, true)]   // ListItem
        [InlineData(50011, true)]   // MenuItem
        [InlineData(50019, true)]   // TabItem
        [InlineData(50031, true)]   // SplitButton
        [InlineData(50020, false)]  // Text
        [InlineData(50006, false)]  // Image
        [InlineData(50026, false)]  // Group
        [InlineData(50033, false)]  // Pane
        public void LegacyAction_OnlyForClickableControlTypes(int controlType, bool expected)
        {
            Assert.Equal(expected, UiAutomationHintKindSelector.IsLegacyActionControlType(controlType));
        }

        [Fact]
        public void LegacyDefaultAction_IsIgnored_WhenNotAllowed()
        {
            Assert.Equal(K.None, UiAutomationHintKindSelector.Select(C.LegacyDefaultActionAvailable));
            Assert.Equal(K.None, UiAutomationHintKindSelector.Select(C.LegacyDefaultActionAvailable, false));
        }

        /// <summary>        /// Every flag combination matches the pre-cache logic: GetCurrentPattern probes in order Invoke,
        /// Toggle, SelectionItem, ExpandCollapse, then writable Value, then writable RangeValue.
        /// </summary>
        [Fact]
        public void AllCombinations_MatchOriginalPatternProbeOrder()
        {
            for (var i = 0; i < AllFlagCombinations; ++i)
            {
                var capabilities = (C)i;
                Assert.True(
                    OriginalProbeOrder(capabilities) == UiAutomationHintKindSelector.Select(capabilities),
                    string.Format("Mismatch for {0}", capabilities));
            }
        }

        private static K OriginalProbeOrder(C c)
        {
            if (c.HasFlag(C.InvokeAvailable)) return K.Invoke;
            if (c.HasFlag(C.ToggleAvailable)) return K.Toggle;
            if (c.HasFlag(C.SelectionItemAvailable)) return K.Select;
            if (c.HasFlag(C.ExpandCollapseAvailable)) return K.ExpandCollapse;
            if (c.HasFlag(C.ValueAvailable) && !c.HasFlag(C.ValueReadOnly)) return K.Focus;
            if (c.HasFlag(C.RangeValueAvailable) && !c.HasFlag(C.RangeValueReadOnly)) return K.Focus;
            return K.None;
        }
    }
}
