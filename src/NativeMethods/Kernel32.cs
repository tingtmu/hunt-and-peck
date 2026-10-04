using System.Runtime.InteropServices;

namespace HuntAndPeck.NativeMethods
{
    public static class Kernel32
    {
        /// <remarks>Cannot fail</remarks>
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
    }
}
