using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HuntAndPeck.NativeMethods
{
    /// <summary>
    /// Synthesized mouse clicks, for elements whose UI Automation actions do nothing (Windows 11 taskbar app buttons)
    /// </summary>
    public static class MouseInput
    {
        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

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

        /// <remarks>MOUSEINPUT is the largest member of the native INPUT union, so it alone gives the right size</remarks>
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        /// <summary>Shift, Ctrl, Alt, left Win, right Win: held, they would turn the click into e.g. Shift+click</summary>
        private static readonly int[] ModifierKeys = { 0x10, 0x11, 0x12, 0x5B, 0x5C };

        /// <summary>Longest wait for held modifier keys (e.g. from the hotkey) to be released</summary>
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
        /// Left-clicks at the screen point (physical pixels), then moves the cursor back where it was
        /// </summary>
        /// <remarks>Waits for held modifier keys to be released first, so they don't change what the click does</remarks>
        /// <exception cref="InvalidOperationException">
        /// A modifier key stayed held, the cursor could not be moved, or the input was blocked (e.g. by UIPI)
        /// </exception>
        public static void LeftClick(int x, int y)
        {
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
                var inputs = new[] { Button(MOUSEEVENTF_LEFTDOWN), Button(MOUSEEVENTF_LEFTUP) };
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

        private static INPUT Button(uint flags)
        {
            return new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = flags } };
        }
    }
}
