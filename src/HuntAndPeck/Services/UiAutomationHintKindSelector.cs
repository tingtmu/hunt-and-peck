using System;
using System.Collections.Generic;
using UIAutomationClient;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Decides which hint an element gets from its cached capabilities. Pure; no UI Automation calls.
    /// </summary>
    internal static class UiAutomationHintKindSelector
    {
        /// <summary>
        /// Control types that get a LegacyIAccessible default action hint: clickable items. Text, images, groups
        /// and panes often report a default action too (inherited from a clickable parent) and would only
        /// duplicate their parent's hint.
        /// </summary>
        private static readonly HashSet<int> s_legacyActionControlTypes = new HashSet<int>
        {
            UIA_ControlTypeIds.UIA_ButtonControlTypeId,
            UIA_ControlTypeIds.UIA_ListItemControlTypeId,
            UIA_ControlTypeIds.UIA_MenuItemControlTypeId,
            UIA_ControlTypeIds.UIA_TabItemControlTypeId,
            UIA_ControlTypeIds.UIA_SplitButtonControlTypeId,
        };

        /// <summary>True if an element of this control type may get a LegacyIAccessible default action hint</summary>
        public static bool IsLegacyActionControlType(int controlTypeId)
        {
            return s_legacyActionControlTypes.Contains(controlTypeId);
        }

        /// <summary>UI Automation FrameworkId of Qt (Qt Widgets and Qt Quick) providers</summary>
        public const string QtFrameworkId = "Qt";

        /// <summary>
        /// Control types of Qt item view cells (QListView, QTreeView, QTableView items). Their Invoke and
        /// SelectionItem actions don't emit the view's clicked/activated signals that apps act on, e.g. LINE's
        /// chat list opens a chat on click only.
        /// </summary>
        private static readonly HashSet<int> s_qtItemControlTypes = new HashSet<int>
        {
            UIA_ControlTypeIds.UIA_ListItemControlTypeId,
            UIA_ControlTypeIds.UIA_TreeItemControlTypeId,
            UIA_ControlTypeIds.UIA_DataItemControlTypeId,
        };

        /// <summary>True if the framework is Qt; only then does <see cref="PrefersClick"/> need the control type</summary>
        public static bool IsQt(string frameworkId)
        {
            return string.Equals(frameworkId, QtFrameworkId, StringComparison.Ordinal);
        }

        /// <summary>
        /// True if an actionable element should be clicked with the mouse rather than through its UI Automation
        /// pattern: a Qt item view cell (see <see cref="s_qtItemControlTypes"/>). Other Qt elements, e.g.
        /// buttons, keep their pattern action.
        /// </summary>
        public static bool PrefersClick(string frameworkId, int controlTypeId)
        {
            return IsQt(frameworkId) && s_qtItemControlTypes.Contains(controlTypeId);
        }

        /// <summary>
        /// Precedence: Invoke, Toggle, SelectionItem, ExpandCollapse, then Focus for a writable Value or
        /// writable RangeValue element; otherwise no hint.
        /// </summary>
        public static UiAutomationHintKind Select(UiAutomationCapabilities capabilities)
        {
            return Select(capabilities, false);
        }

        /// <summary>
        /// As <see cref="Select(UiAutomationCapabilities)"/>, then, if <paramref name="allowLegacyDefaultAction"/>,
        /// the LegacyIAccessible default action as a last resort (e.g. Windows 11 taskbar app buttons, which
        /// support no action pattern)
        /// </summary>
        public static UiAutomationHintKind Select(UiAutomationCapabilities capabilities, bool allowLegacyDefaultAction)
        {
            var kind = SelectPattern(capabilities);
            if (kind == UiAutomationHintKind.None && allowLegacyDefaultAction
                && Has(capabilities, UiAutomationCapabilities.LegacyDefaultActionAvailable))
            {
                return UiAutomationHintKind.LegacyDefaultAction;
            }
            return kind;
        }

        private static UiAutomationHintKind SelectPattern(UiAutomationCapabilities capabilities)
        {
            if (Has(capabilities, UiAutomationCapabilities.InvokeAvailable))
            {
                return UiAutomationHintKind.Invoke;
            }
            if (Has(capabilities, UiAutomationCapabilities.ToggleAvailable))
            {
                return UiAutomationHintKind.Toggle;
            }
            if (Has(capabilities, UiAutomationCapabilities.SelectionItemAvailable))
            {
                return UiAutomationHintKind.Select;
            }
            if (Has(capabilities, UiAutomationCapabilities.ExpandCollapseAvailable))
            {
                return UiAutomationHintKind.ExpandCollapse;
            }
            return IsFocusable(capabilities) ? UiAutomationHintKind.Focus : UiAutomationHintKind.None;
        }

        private static bool IsFocusable(UiAutomationCapabilities capabilities)
        {
            var writableValue = Has(capabilities, UiAutomationCapabilities.ValueAvailable)
                && !Has(capabilities, UiAutomationCapabilities.ValueReadOnly);
            var writableRangeValue = Has(capabilities, UiAutomationCapabilities.RangeValueAvailable)
                && !Has(capabilities, UiAutomationCapabilities.RangeValueReadOnly);
            return writableValue || writableRangeValue;
        }

        private static bool Has(UiAutomationCapabilities capabilities, UiAutomationCapabilities flag)
        {
            return (capabilities & flag) == flag;
        }
    }
}
