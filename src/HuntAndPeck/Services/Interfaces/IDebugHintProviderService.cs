using System;
using System.Threading.Tasks;
using HuntAndPeck.Models;

namespace HuntAndPeck.Services.Interfaces
{
    public interface IDebugHintProviderService
    {
        /// <returns>The debug hint session, or null if there is no foreground window or enumeration failed</returns>
        Task<HintSession> EnumDebugHintsAsync();

        /// <returns>The debug hint session, or null if enumeration failed or timed out</returns>
        Task<HintSession> EnumDebugHintsAsync(IntPtr hWnd);
    }
}
