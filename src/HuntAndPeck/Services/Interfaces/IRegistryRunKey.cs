namespace HuntAndPeck.Services.Interfaces
{
    /// <summary>
    /// Minimal access to the per-user autostart registry values, so the startup logic can be tested
    /// without touching the real registry
    /// </summary>
    /// <remarks>
    /// Implementations may throw <see cref="System.Security.SecurityException"/>,
    /// <see cref="System.UnauthorizedAccessException"/> or <see cref="System.IO.IOException"/>.
    /// </remarks>
    internal interface IRegistryRunKey
    {
        /// <summary>
        /// Reads a string value of the Run key, else null if absent (or not a string)
        /// </summary>
        string GetRunValue(string name);

        void SetRunValue(string name, string data);

        /// <summary>
        /// Deletes a value of the Run key; no-op if absent
        /// </summary>
        void DeleteRunValue(string name);

        /// <summary>
        /// Reads the binary StartupApproved\Run value (Task Manager's enabled/disabled flag), else null if absent
        /// </summary>
        byte[] GetStartupApprovedValue(string name);

        void SetStartupApprovedValue(string name, byte[] data);

        /// <summary>
        /// Deletes a value of the StartupApproved\Run key; no-op if absent
        /// </summary>
        void DeleteStartupApprovedValue(string name);
    }
}
