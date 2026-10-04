using HuntAndPeck.NativeMethods;

namespace HuntAndPeck.Services.Interfaces
{
    /// <summary>
    /// A hotkey that could not be registered
    /// </summary>
    internal sealed class HotKeyFailure
    {
        /// <summary>ERROR_HOTKEY_ALREADY_REGISTERED: another app owns the key combination</summary>
        public const int ErrorHotKeyAlreadyRegistered = 1409;

        /// <param name="hotKey">The hotkey</param>
        /// <param name="errorCode">The Win32 error of RegisterHotKey</param>
        /// <param name="isRestore">True if re-registering a previously active hotkey failed (it is now inactive)</param>
        public HotKeyFailure(HotKey hotKey, int errorCode, bool isRestore = false)
        {
            HotKey = hotKey;
            ErrorCode = errorCode;
            IsRestore = isRestore;
        }

        public HotKey HotKey { get; }

        /// <summary>The Win32 error of RegisterHotKey</summary>
        public int ErrorCode { get; }

        /// <summary>
        /// True if a previously active hotkey could not be registered again (after a rollback or suspension),
        /// so it no longer works; false if a new hotkey was rejected
        /// </summary>
        public bool IsRestore { get; }

        /// <summary>
        /// User facing description, e.g. "Alt+; is already used by another app."
        /// </summary>
        public string Message
        {
            get
            {
                if (ErrorCode != ErrorHotKeyAlreadyRegistered)
                {
                    return string.Format("{0} could not be registered (error {1}).", HotKey, ErrorCode);
                }
                return HotKey.Modifier.HasFlag(KeyModifier.Windows)
                    ? string.Format("{0} is reserved by Windows or used by another app.", HotKey)
                    : string.Format("{0} is already used by another app.", HotKey);
            }
        }
    }
}
