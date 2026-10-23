using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace HuntAndPeck.NativeMethods
{
    /// <summary>How <see cref="MouseInput.Click"/> clicks</summary>
    public enum MouseAction
    {
        LeftClick,
        RightClick,
        DoubleClick,

        /// <summary>No click: the cursor moves onto the element and stays there (<see cref="MouseInput.MoveTo"/>)</summary>
        Hover,
    }

    /// <summary>
    /// Synthesized mouse clicks, for elements whose UI Automation actions do nothing (Windows 11 taskbar app buttons),
    /// and for clicks UI Automation has no action for (right-click, double-click)
    /// </summary>
    public static class MouseInput
    {
        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_HWHEEL = 0x1000;

        /// <summary>Time the target gets to read the pressed/released input before the cursor moves back</summary>
        private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(30);

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <remarks>MOUSEINPUT is the largest member of the native INPUT union, so it gives the union's size</remarks>
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_RCONTROL = 0xA3;

        /// <summary>
        /// Left/right Shift and Ctrl: still held from typing the last letter (they chose the click), so they are
        /// released for the click rather than waited for
        /// </summary>
        private static readonly ushort[] ReleasedModifierKeys = { 0xA0, 0xA1, 0xA2, VK_RCONTROL };

        /// <summary>Shift, Ctrl, Alt, left Win, right Win: held, they would turn the click into e.g. Shift+click</summary>
        private static readonly int[] ModifierKeys = { 0x10, 0x11, 0x12, 0x5B, 0x5C };

        /// <summary>
        /// Longest wait for held modifier keys (e.g. Alt from the hotkey) to be released. Alt and Win are not
        /// released by a synthesized key-up: that would open the menu bar or the Start menu.
        /// </summary>
        private static readonly TimeSpan ModifierReleaseTimeout = TimeSpan.FromSeconds(1);

        private static readonly TimeSpan ModifierPollInterval = TimeSpan.FromMilliseconds(20);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT point);

        /// <summary>Physical pixels for a per-monitor DPI aware caller</summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        /// <summary>
        /// Clicks at the screen point (physical pixels), then moves the cursor back where it was
        /// </summary>
        /// <remarks>
        /// Held modifier keys must not change what the click does: a held Shift or Ctrl (e.g. the Ctrl that chose a
        /// right-click) is released with a synthesized key-up, other modifiers are waited for. A double-click is sent
        /// as one input batch, well within the double-click time.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// A modifier key stayed held, the cursor could not be moved, or the input was blocked (e.g. by UIPI)
        /// </exception>
        public static void Click(int x, int y, MouseAction action)
        {
            ReleaseShiftAndCtrl();
            if (!WaitForModifiersReleased())
            {
                throw new InvalidOperationException(string.Format("a modifier key was still held after {0} ms; not clicking", ModifierReleaseTimeout.TotalMilliseconds));
            }

            POINT previous;
            var restore = GetCursorPos(out previous);
            if (!SetCursorPos(x, y))
            {
                throw new InvalidOperationException(string.Format("SetCursorPos failed, error {0}", Marshal.GetLastWin32Error()));
            }

            try
            {
                var inputs = Inputs(action);
                var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
                if (sent != inputs.Length)
                {
                    throw new InvalidOperationException(string.Format("SendInput sent {0} of {1} inputs, error {2}", sent, inputs.Length, Marshal.GetLastWin32Error()));
                }
                Thread.Sleep(SettleDelay);
            }
            finally
            {
                if (restore)
                {
                    SetCursorPos(previous.X, previous.Y);
                }
            }
        }

        /// <summary>
        /// Moves the cursor to the screen point (physical pixels) and leaves it there, e.g. to show a tooltip
        /// </summary>
        /// <remarks>Held modifiers (the Alt that chose the hover) are not waited for: no button is pressed</remarks>
        /// <exception cref="InvalidOperationException">The cursor could not be moved</exception>
        public static void MoveTo(int x, int y)
        {
            if (!SetCursorPos(x, y))
            {
                throw new InvalidOperationException(string.Format("SetCursorPos failed, error {0}", Marshal.GetLastWin32Error()));
            }
        }

        /// <summary>
        /// Turns the mouse wheel at the cursor, which scrolls the window under it whichever window has the keyboard
        /// </summary>
        /// <param name="vertical">Wheel delta, positive scrolls up (120 is one notch); 0 for none</param>
        /// <param name="horizontal">Horizontal wheel delta, positive scrolls right; 0 for none</param>
        /// <exception cref="InvalidOperationException">The input was blocked (e.g. by UIPI)</exception>
        public static void Wheel(int vertical, int horizontal)
        {
            var inputs = new List<INPUT>();
            if (vertical != 0)
            {
                inputs.Add(WheelInput(MOUSEEVENTF_WHEEL, vertical));
            }
            if (horizontal != 0)
            {
                inputs.Add(WheelInput(MOUSEEVENTF_HWHEEL, horizontal));
            }
            if (inputs.Count == 0)
            {
                return;
            }
            var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Count)
            {
                throw new InvalidOperationException(string.Format("SendInput sent {0} of {1} wheel inputs, error {2}", sent, inputs.Count, Marshal.GetLastWin32Error()));
            }
        }

        private static INPUT WheelInput(uint flags, int delta)
        {
            // mouseData is a DWORD holding a signed delta
            return new INPUT { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags, mouseData = unchecked((uint)delta) } } };
        }

        /// <summary>
        /// Sends a key-up for each held Shift and Ctrl key. Its later physical release is a harmless second key-up.
        /// If the input is blocked (UIPI), the keys stay held and <see cref="WaitForModifiersReleased"/> times out.
        /// </summary>
        private static void ReleaseShiftAndCtrl()
        {
            var inputs = new List<INPUT>();
            foreach (var key in ReleasedModifierKeys)
            {
                if ((GetAsyncKeyState(key) & 0x8000) != 0)
                {
                    var flags = KEYEVENTF_KEYUP | (key == VK_RCONTROL ? KEYEVENTF_EXTENDEDKEY : 0);
                    inputs.Add(new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = key, dwFlags = flags } } });
                }
            }
            if (inputs.Count > 0)
            {
                SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            }
        }

        /// <returns>True once no modifier key is held, else false if one still is after the timeout</returns>
        private static bool WaitForModifiersReleased()
        {
            var deadline = DateTime.UtcNow + ModifierReleaseTimeout;
            while (IsModifierHeld())
            {
                if (DateTime.UtcNow >= deadline)
                {
                    return false;
                }
                Thread.Sleep(ModifierPollInterval);
            }
            return true;
        }

        private static bool IsModifierHeld()
        {
            foreach (var key in ModifierKeys)
            {
                if ((GetAsyncKeyState(key) & 0x8000) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static INPUT[] Inputs(MouseAction action)
        {
            switch (action)
            {
                case MouseAction.RightClick:
                    return new[] { Button(MOUSEEVENTF_RIGHTDOWN), Button(MOUSEEVENTF_RIGHTUP) };
                case MouseAction.DoubleClick:
                    return new[] { Button(MOUSEEVENTF_LEFTDOWN), Button(MOUSEEVENTF_LEFTUP), Button(MOUSEEVENTF_LEFTDOWN), Button(MOUSEEVENTF_LEFTUP) };
                default:
                    return new[] { Button(MOUSEEVENTF_LEFTDOWN), Button(MOUSEEVENTF_LEFTUP) };
            }
        }

        private static INPUT Button(uint flags)
        {
            return new INPUT { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } } };
        }
    }
}
