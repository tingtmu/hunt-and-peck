using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;

namespace HuntAndPeck.Services.Interfaces
{
    /// <summary>
    /// Provides hints for the entire desktop or a given window handle
    /// </summary>
    /// <remarks>
    /// Enumeration and invocation run off the calling (UI) thread and never block it. Await the returned
    /// tasks from the UI thread to resume there.
    /// </remarks>
    public interface IHintProviderService
    {
        /// <summary>
        /// Enumerate the available hints for the current foreground window
        /// </summary>
        /// <returns>
        /// The hint session containing the available hints, or null if there is no foreground window or
        /// the enumeration failed or timed out (logged)
        /// </returns>
        Task<HintSession> EnumHintsAsync();

        /// <summary>
        /// Enumerate the available hints for the given window
        /// </summary>
        /// <returns>The hint session, or null if enumeration failed or timed out (logged)</returns>
        Task<HintSession> EnumHintsAsync(IntPtr handle);

        /// <summary>
        /// Enumerate the hints of several bar windows (taskbar, status bars) for one overlay covering them all.
        /// Elements without an action pattern but with a LegacyIAccessible default action also get hints.
        /// </summary>
        /// <param name="windows">The windows, first the one owning the session</param>
        /// <param name="monitor">The monitor's bounds, physical pixels; windows are clipped to it</param>
        /// <returns>
        /// The session; its bounds are the union of the windows' clipped bounds and its hint bounds are relative
        /// to it. Null if none of the windows is on the monitor any more. Windows that fail are left out (logged).
        /// </returns>
        Task<HintSession> EnumBarHintsAsync(IReadOnlyList<IntPtr> windows, Rect monitor);

        /// <summary>
        /// Invokes a hint from a session created by this service. Safe to fire and forget.
        /// </summary>
        /// <returns>
        /// A task that completes when the invocation finished, failed or timed out; it never faults. Its result
        /// is false if the action failed in a way a mouse click on the element may fix (see
        /// <see cref="Hint.CreateClickFallback"/>), else true.
        /// </returns>
        Task<bool> InvokeHintAsync(Hint hint);
    }
}
