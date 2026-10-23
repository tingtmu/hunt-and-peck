using System;
using System.Diagnostics;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// After a hover (Alt + last letter), the arrow keys and PgUp/PgDn turn the mouse wheel, so they scroll the pane
    /// under the cursor rather than the one with the keyboard focus. Esc stops; any other key stops and goes on to
    /// its window (so the hotkeys and typing work as usual), as does switching windows. Dispose stops too.
    /// </summary>
    internal sealed class HoverScroller : IDisposable
    {
        /// <summary>One wheel notch</summary>
        public const int WheelDelta = 120;

        /// <summary>Wheel notches for PgUp/PgDn</summary>
        public const int PageNotches = 5;

        private const int VkEscape = 0x1B;
        private const int VkPrior = 0x21;
        private const int VkNext = 0x22;
        private const int VkLeft = 0x25;
        private const int VkUp = 0x26;
        private const int VkRight = 0x27;
        private const int VkDown = 0x28;

        /// <summary>Shift, Ctrl, Alt (generic, left and right) and left/right Win: held for another key, so they pass</summary>
        private static readonly int[] ModifierKeys = { 0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C };

        private readonly Action<int, int> _wheel;
        private readonly Func<IntPtr> _foreground;
        private readonly IntPtr _window;
        private IDisposable _hook;

        /// <param name="installHook">Installs a keyboard hook calling the handler for each key press (see <see cref="NativeMethods.KeyboardHook"/>)</param>
        /// <param name="wheel">Turns the wheel by (vertical, horizontal) deltas (see <see cref="NativeMethods.MouseInput.Wheel"/>)</param>
        /// <param name="foreground">The foreground window; scrolling stops once it is no longer the one at the start</param>
        /// <exception cref="InvalidOperationException">The hook could not be installed</exception>
        public HoverScroller(Func<Func<int, bool>, IDisposable> installHook, Action<int, int> wheel, Func<IntPtr> foreground)
        {
            _wheel = wheel;
            _foreground = foreground;
            _window = foreground();
            _hook = installHook(OnKeyDown);
        }

        /// <summary>True until Esc, another key, a window switch or Dispose stopped the scrolling</summary>
        public bool Active => _hook != null;

        /// <returns>True if the key scrolled (or was Esc) and must not reach the target</returns>
        private bool OnKeyDown(int virtualKey)
        {
            if (!Active)
            {
                return false;
            }
            if (Array.IndexOf(ModifierKeys, virtualKey) >= 0)
            {
                return false;
            }
            if (_foreground() != _window)
            {
                Trace.TraceInformation("Hover scroll: the foreground window changed, stopping");
                Dispose();
                return false;
            }

            switch (virtualKey)
            {
                case VkUp: return Scroll(WheelDelta, 0);
                case VkDown: return Scroll(-WheelDelta, 0);
                case VkLeft: return Scroll(0, -WheelDelta);
                case VkRight: return Scroll(0, WheelDelta);
                case VkPrior: return Scroll(PageNotches * WheelDelta, 0);
                case VkNext: return Scroll(-PageNotches * WheelDelta, 0);
                case VkEscape:
                    Dispose();
                    return true;
                default:
                    Dispose();
                    return false;
            }
        }

        private bool Scroll(int vertical, int horizontal)
        {
            try
            {
                _wheel(vertical, horizontal);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                // E.g. UIPI blocks input to an elevated window: the keys work as without scrolling
                Trace.TraceWarning("Hover scroll: turning the wheel failed, stopping: {0}", ex.Message);
                Dispose();
                return false;
            }
        }

        public void Dispose()
        {
            _hook?.Dispose();
            _hook = null;
        }
    }
}
