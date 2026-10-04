using System;
using System.Collections.Generic;

namespace HuntAndPeck.Services.Interfaces
{
    /// <summary>
    /// Service for listening to global keyboard shortcuts
    /// </summary>
    /// <remarks>
    /// Setting a hotkey registers it; check <see cref="HotKey.IsRegistered"/> afterwards, since registration
    /// fails if another app already owns the key combination (failures are logged).
    /// </remarks>
    internal interface IKeyListenerService
    {
        event EventHandler OnHotKeyActivated;
        event EventHandler OnTaskbarHotKeyActivated;
        event EventHandler OnDebugHotKeyActivated;

        HotKey TaskbarHotKey { get; set; }
        HotKey HotKey { get; set; }
        HotKey DebugHotKey { get; set; }

        /// <summary>
        /// Replaces the main and taskbar hotkeys all-or-nothing: if either new hotkey cannot be registered,
        /// both keep their current hotkey
        /// </summary>
        /// <returns>
        /// The new hotkeys that failed to register (then nothing changed), plus with
        /// <see cref="HotKeyFailure.IsRestore"/> any previously active hotkey that could not be registered again;
        /// empty on full success
        /// </returns>
        /// <remarks>Resumes suspended hotkeys first</remarks>
        IReadOnlyList<HotKeyFailure> ReplaceHotKeys(HotKey hotKey, HotKey taskbarHotKey);

        /// <summary>
        /// Temporarily unregisters the registered hotkeys (e.g. while the user records a new one, which a
        /// registered hotkey would otherwise swallow), or registers them again. Hotkeys set while suspended are
        /// registered on resume; hotkeys that were already unavailable are not retried.
        /// </summary>
        /// <returns>On resume, the hotkeys that could not be registered again (restore failures)</returns>
        IReadOnlyList<HotKeyFailure> SetSuspended(bool suspended);
    }
}
