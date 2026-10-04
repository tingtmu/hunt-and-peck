using System;

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
    }
}
