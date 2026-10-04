using System;
using System.Threading.Tasks;
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
        /// Invokes a hint from a session created by this service. Safe to fire and forget.
        /// </summary>
        /// <returns>A task that completes when the invocation finished, failed or timed out; it never faults</returns>
        Task InvokeHintAsync(Hint hint);
    }
}
