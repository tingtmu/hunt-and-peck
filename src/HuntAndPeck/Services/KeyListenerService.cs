using HuntAndPeck.NativeMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    internal class KeyListenerService : Form, IKeyListenerService
    {
        /// <summary>ERROR_HOTKEY_ALREADY_REGISTERED: another app owns the key combination</summary>
        public const int ErrorHotKeyAlreadyRegistered = HotKeyFailure.ErrorHotKeyAlreadyRegistered;

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
        private bool _suspended;

        /// <summary>
        /// While suspended: the hotkeys to register on resume (those registered when suspending, plus any
        /// set meanwhile). Hotkeys that were already unavailable are not retried.
        /// </summary>
        private readonly List<HotKey> _toResume = new List<HotKey>();

        /// <summary>
        /// Makes <paramref name="value"/> the hotkey in place of <paramref name="current"/>, registering it
        /// now or, while suspended, on resume
        /// </summary>
        /// <returns><paramref name="value"/></returns>
        private HotKey Assign(HotKey current, HotKey value)
        {
            if (current != null && !ReferenceEquals(current, value))
            {
                Unregister(current);
                _toResume.Remove(current);
            }
            if (value == null)
            {
                return null;
            }

            Unregister(value);
            if (!_suspended)
            {
                Register(value);
            }
            else if (!_toResume.Contains(value))
            {
                _toResume.Add(value);
            }
            return value;
        }

        /// <summary>
        /// Registers the hotkey under a new id, logging failures
        /// </summary>
        /// <returns>0 on success, else the Win32 error</returns>
        private int Register(HotKey hotKey)
        {
            hotKey.RegistrationId = _hotkeyIdCounter++;
            // MOD_NOREPEAT: holding the combination down does not fire repeatedly
            var modifiers = (uint)(hotKey.Modifier | KeyModifier.NoRepeat);
            hotKey.IsRegistered = User32.RegisterHotKey(Handle, hotKey.RegistrationId, modifiers, (uint)hotKey.Keys);
            if (hotKey.IsRegistered)
            {
                Trace.TraceInformation("Registered hotkey {0}", hotKey);
                return 0;
            }

            var error = Marshal.GetLastWin32Error();
            var reason = error == ErrorHotKeyAlreadyRegistered ? "already registered by another app" : "failed";
            Trace.TraceWarning("RegisterHotKey {0} {1}, error {2}", hotKey, reason, error);
            // Guard against a failure that did not set the last error
            return error == 0 ? -1 : error;
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
                _hotKey = Assign(_hotKey, value);
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
                _taskbarHotKey = Assign(_taskbarHotKey, value);
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
                _debugHotKey = Assign(_debugHotKey, value);
            }
        }

        public IReadOnlyList<HotKeyFailure> ReplaceHotKeys(HotKey hotKey, HotKey taskbarHotKey)
        {
            var resumeFailures = SetSuspended(false);
            var changes = new[]
            {
                Tuple.Create(_hotKey, hotKey),
                Tuple.Create(_taskbarHotKey, taskbarHotKey),
            };
            var failures = resumeFailures.Concat(HotKeyTransaction.Apply(changes, Register, Unregister)).ToList();
            if (failures.Any(x => !x.IsRestore))
            {
                return failures;
            }

            // Keep the current objects for unchanged combinations: they hold the registration
            _hotKey = HotKeyTransaction.SameCombination(_hotKey, hotKey) ? _hotKey : hotKey;
            _taskbarHotKey = HotKeyTransaction.SameCombination(_taskbarHotKey, taskbarHotKey) ? _taskbarHotKey : taskbarHotKey;
            return failures;
        }

        public IReadOnlyList<HotKeyFailure> SetSuspended(bool suspended)
        {
            var failures = new List<HotKeyFailure>();
            if (_suspended == suspended)
            {
                return failures;
            }

            _suspended = suspended;
            Trace.TraceInformation("Hotkeys {0}", suspended ? "suspended" : "resumed");
            if (suspended)
            {
                _toResume.Clear();
                _toResume.AddRange(new[] { _hotKey, _taskbarHotKey, _debugHotKey }.Where(x => x != null && x.IsRegistered));
                _toResume.ForEach(Unregister);
                return failures;
            }

            foreach (var hotKey in _toResume)
            {
                var error = Register(hotKey);
                if (error != 0)
                {
                    failures.Add(new HotKeyFailure(hotKey, error, isRestore: true));
                }
            }
            _toResume.Clear();
            return failures;
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
            // Mask MOD_NOREPEAT, which is passed to RegisterHotKey but is not part of the combination
            return hotKey != null && e.Key == hotKey.Keys && (e.Modifiers & ~KeyModifier.NoRepeat) == hotKey.Modifier;
        }

        private void Unregister(HotKey hotKey)
        {
            if (hotKey == null || !hotKey.IsRegistered)
            {
                return;
            }

            if (!User32.UnregisterHotKey(Handle, hotKey.RegistrationId))
            {
                Trace.TraceWarning("UnregisterHotKey {0} (id {1}) failed, error {2}", hotKey, hotKey.RegistrationId, Marshal.GetLastWin32Error());
            }
            hotKey.IsRegistered = false;
        }
    }
}
