using System.Runtime.InteropServices;

namespace HuntAndPeck.NativeMethods
{
    /// <summary>
    /// Monitor bounds for <see cref="User32.GetMonitorInfo"/>; same layout (40 bytes) in 32- and 64-bit processes
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        /// <summary>
        /// Creates the struct with cbSize set, as GetMonitorInfo requires
        /// </summary>
        public static MONITORINFO Create()
        {
            return new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
        }
    }
}
