using HuntAndPeck.NativeMethods;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    internal class KeyListenerService : Form, IKeyListenerService
    {
        /// <summary>ERROR_HOTKEY_ALREADY_REGISTERED: another app owns the key combination</summary>
        public const int ErrorHotKeyAlreadyRegistered = 1409;

        public event EventHandler OnHotKeyActivated;
        public event EventHandler OnTaskbarHotKeyActivated;
        public event EventHandler OnDebugHotKeyActivated;

        /// <summary>
        /// Counter for assigning ids to identify the hot key registrations. Starts at 1 so that 0 can mean
        /// "never registered".
        /// </summary>
        private int _hotkeyIdCounter = 1;

        private HotKey _hotKey;
        private HotKey _taskbarHotKey;
        private HotKey _debugHotKey;

        /// <summary>
        /// Re-registers the current hotkey, unregistering any previous key
        /// </summary>
        /// <returns>True if the hotkey was registered</returns>
        private bool ReRegisterHotKey(HotKey hotKey)
        {
            // Already registered, have to unregister first
            if (hotKey.IsRegistered && !User32.UnregisterHotKey(Handle, hotKey.RegistrationId))
            {
                Trace.TraceWarning("UnregisterHotKey {0} (id {1}) failed, error {2}", hotKey, hotKey.RegistrationId, Marshal.GetLastWin32Error());
            }

            hotKey.RegistrationId = _hotkeyIdCounter++;
            hotKey.IsRegistered = User32.RegisterHotKey(Handle, hotKey.RegistrationId, (uint)hotKey.Modifier, (uint)hotKey.Keys);
            if (hotKey.IsRegistered)
            {
                Trace.TraceInformation("Registered hotkey {0}", hotKey);
                return true;
            }

            var error = Marshal.GetLastWin32Error();
            var reason = error == ErrorHotKeyAlreadyRegistered ? "already registered by another app" : "failed";
            Trace.TraceWarning("RegisterHotKey {0} {1}, error {2}", hotKey, reason, error);
            return false;
        }

        /// <summary>
        /// Gets/sets the current hotkey
        /// </summary>
        /// <remarks>Changing this will cause the current hotkey to be unregistered</remarks>
        public HotKey HotKey
        {
            get
            {
                return _hotKey;
            }
            set
            {
                _hotKey = value;
                ReRegisterHotKey(_hotKey);
            }
        }

        /// <summary>
        /// Gets/sets the current task bar hotkey
        /// </summary>
        /// <remarks>Changing this will cause the current hotkey to be unregistered</remarks>
        public HotKey TaskbarHotKey
        {
            get
            {
                return _taskbarHotKey;
            }
            set
            {
                _taskbarHotKey = value;
                ReRegisterHotKey(_taskbarHotKey);
            }
        }

        public HotKey DebugHotKey
        {
            get
            {
                return _debugHotKey;
            }
            set
            {
                _debugHotKey = value;
                ReRegisterHotKey(_debugHotKey);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Constants.WM_HOTKEY)
            {
                var e = new HotKeyEventArgs(m.LParam);

                if (Matches(_hotKey, e))
                {
                    OnHotKeyActivated?.Invoke(this, EventArgs.Empty);
                }

                if (Matches(_taskbarHotKey, e))
                {
                    OnTaskbarHotKeyActivated?.Invoke(this, EventArgs.Empty);
                }

                if (Matches(_debugHotKey, e))
                {
                    OnDebugHotKeyActivated?.Invoke(this, EventArgs.Empty);
                }
            }

            base.WndProc(ref m);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Ensures that the window will never be displayed
            base.SetVisibleCore(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && IsHandleCreated)
            {
                Unregister(_hotKey);
                Unregister(_taskbarHotKey);
                Unregister(_debugHotKey);
            }
            base.Dispose(disposing);
        }

        private static bool Matches(HotKey hotKey, HotKeyEventArgs e)
        {
            return hotKey != null && e.Key == hotKey.Keys && e.Modifiers == hotKey.Modifier;
        }

        private void Unregister(HotKey hotKey)
        {
            if (hotKey == null || !hotKey.IsRegistered)
            {
                return;
            }

            if (!User32.UnregisterHotKey(Handle, hotKey.RegistrationId))
            {
                Trace.TraceWarning("UnregisterHotKey {0} failed on exit, error {1}", hotKey, Marshal.GetLastWin32Error());
            }
            hotKey.IsRegistered = false;
        }
    }
}
