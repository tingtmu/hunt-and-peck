using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services
{
    /// <summary>A visible top-level window, as far as menu detection needs it</summary>
    internal sealed class TopLevelWindow
    {
        public TopLevelWindow(IntPtr handle, uint processId, string className, bool isPopup)
        {
            Handle = handle;
            ProcessId = processId;
            ClassName = className;
            IsPopup = isPopup;
        }

        public IntPtr Handle { get; }
        public uint ProcessId { get; }
        public string ClassName { get; }

        /// <summary>WS_POPUP without a caption: menus, dropdown lists, tooltips; not dialogs</summary>
        public bool IsPopup { get; }
    }

    internal interface ITopLevelWindowSource
    {
        /// <summary>The visible top-level windows</summary>
        IReadOnlyList<TopLevelWindow> VisibleWindows();

        /// <summary>The process owning the window, else 0</summary>
        uint ProcessOf(IntPtr hWnd);
    }

    internal sealed class TopLevelWindowSource : ITopLevelWindowSource
    {
        public IReadOnlyList<TopLevelWindow> VisibleWindows()
        {
            var result = new List<TopLevelWindow>();
            WindowEnumeration.EnumWindows((hWnd, lParam) =>
            {
                if (WindowEnumeration.IsWindowVisible(hWnd))
                {
                    var style = WindowEnumeration.GetStyle(hWnd);
                    var isPopup = (style & WindowEnumeration.WS_POPUP) != 0
                        && (style & WindowEnumeration.WS_CAPTION) != WindowEnumeration.WS_CAPTION;
                    result.Add(new TopLevelWindow(hWnd, ProcessOf(hWnd), WindowEnumeration.GetClassName(hWnd), isPopup));
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        public uint ProcessOf(IntPtr hWnd)
        {
            uint processId;
            WindowEnumeration.GetWindowThreadProcessId(hWnd, out processId);
            return processId;
        }
    }

    /// <summary>
    /// Notices a menu or dropdown that a hint's action opened: a new popup window of the same app. Menus close
    /// when their app loses the foreground, so their hints need the non-activating menu overlay.
    /// </summary>
    internal sealed class MenuPopupWatcher
    {
        /// <summary>How long after the action a menu may take to appear</summary>
        public static readonly TimeSpan AppearTimeout = TimeSpan.FromMilliseconds(500);

        public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);

        /// <summary>Time a menu gets to finish opening (animation, submenu windows) before it is scanned</summary>
        public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>Popup windows that are never menus: drop shadows and tooltips</summary>
        private static readonly HashSet<string> s_ignoredClasses = new HashSet<string>(StringComparer.Ordinal)
        {
            "SysShadow",
            "tooltips_class32",
        };

        private readonly ITopLevelWindowSource _windows;
        private readonly Func<TimeSpan, Task> _delay;
        private readonly Func<TimeSpan> _now;

        public MenuPopupWatcher(ITopLevelWindowSource windows, Func<TimeSpan, Task> delay, Func<TimeSpan> now)
        {
            _windows = windows;
            _delay = delay;
            _now = now;
        }

        /// <summary>The visible top-level windows now, to tell new popups from existing ones later</summary>
        public HashSet<IntPtr> Snapshot()
        {
            return new HashSet<IntPtr>(_windows.VisibleWindows().Select(x => x.Handle));
        }

        /// <param name="owner">A window of the app the action was done in</param>
        /// <param name="before">The <see cref="Snapshot"/> taken before the action</param>
        /// <returns>The new popup windows of the app, else an empty list if none appeared in time</returns>
        public async Task<List<IntPtr>> WaitForNewAsync(IntPtr owner, ISet<IntPtr> before)
        {
            var processId = _windows.ProcessOf(owner);
            if (processId == 0)
            {
                return new List<IntPtr>();
            }

            var appeared = await PollingWait.UntilAsync(() => NewPopups(processId, before).Count > 0, PollInterval, AppearTimeout, _delay, _now);
            if (!appeared)
            {
                return new List<IntPtr>();
            }

            await _delay(SettleDelay);
            return NewPopups(processId, before);
        }

        private List<IntPtr> NewPopups(uint processId, ISet<IntPtr> before)
        {
            return _windows.VisibleWindows()
                .Where(x => x.ProcessId == processId && x.IsPopup && !before.Contains(x.Handle) && !s_ignoredClasses.Contains(x.ClassName))
                .Select(x => x.Handle)
                .ToList();
        }

        /// <summary>True while any of the windows is still shown</summary>
        public bool AnyVisible(IEnumerable<IntPtr> windows)
        {
            var visible = new HashSet<IntPtr>(_windows.VisibleWindows().Select(x => x.Handle));
            return windows.Any(visible.Contains);
        }
    }
}
