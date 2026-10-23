using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HuntAndPeck.NativeMethods
{
    /// <summary>
    /// A low-level keyboard hook (WH_KEYBOARD_LL): sees key presses meant for any window, without taking the
    /// foreground. Used while hints are shown over a menu, which would close if HAP took the foreground.
    /// </summary>
    /// <remarks>
    /// The callback runs on the installing thread, which must pump messages (a WPF UI thread does). It must return
    /// quickly: Windows skips a hook that takes longer than LowLevelHooksTimeout and passes the key on.
    /// Dispose removes the hook.
    /// </remarks>
    public sealed class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>True if the key is held now, whichever window has the keyboard</summary>
        public static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private readonly Func<int, bool> _onKeyDown;

        // Kept in a field: the native hook calls it after this constructor returns, so it must not be collected
        private readonly HookProc _proc;
        private IntPtr _hook;

        /// <param name="onKeyDown">
        /// Called with the virtual-key code of each key press; returns true to swallow the key, false to pass it on
        /// </param>
        /// <exception cref="InvalidOperationException">The hook could not be installed</exception>
        public KeyboardHook(Func<int, bool> onKeyDown)
        {
            _onKeyDown = onKeyDown;
            _proc = Callback;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
            {
                throw new InvalidOperationException(string.Format("SetWindowsHookEx failed, error {0}", Marshal.GetLastWin32Error()));
            }
        }

        private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            var message = wParam.ToInt32();
            if (code >= 0 && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                var key = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                try
                {
                    if (_onKeyDown((int)key.vkCode))
                    {
                        return new IntPtr(1);
                    }
                }
                catch (Exception ex)
                {
                    // An exception must not escape into the native hook chain; the key then reaches the target
                    Trace.TraceError("Keyboard hook handler failed: {0}", ex);
                }
            }
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero && !UnhookWindowsHookEx(_hook))
            {
                Trace.TraceWarning("UnhookWindowsHookEx failed, error {0}", Marshal.GetLastWin32Error());
            }
            _hook = IntPtr.Zero;
        }
    }
}
