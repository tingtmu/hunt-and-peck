using System;
using System.Diagnostics;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Start with Windows via the per-user Run key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run),
    /// honouring the enabled/disabled flag Task Manager keeps under Explorer\StartupApproved\Run
    /// </summary>
    /// <remarks>
    /// The Run value is the quoted exe path without arguments, which starts the normal tray mode.
    /// </remarks>
    internal sealed class StartupRegistrationService : IStartupRegistrationService
    {
        public const string ValueName = "HuntAndPeck";

        /// <summary>
        /// StartupApproved value for "enabled": first byte 0x02, the rest (a FILETIME of when it was disabled,
        /// unused when enabled) zero; the same bytes Task Manager writes when enabling an item
        /// </summary>
        private static readonly byte[] ApprovedEnabled = { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private readonly IRegistryRunKey _registry;
        private readonly string _exePath;
        private readonly Func<string, bool> _fileExists;

        /// <param name="registry">Registry access</param>
        /// <param name="exePath">Full path of the running exe</param>
        /// <param name="fileExists">File existence check (System.IO.File.Exists)</param>
        public StartupRegistrationService(IRegistryRunKey registry, string exePath, Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new ArgumentException("Exe path is required", nameof(exePath));
            }
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _exePath = exePath;
            _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        }

        public bool IsEnabled
        {
            get
            {
                var registeredPath = ParseExePath(_registry.GetRunValue(ValueName));
                return registeredPath != null
                    && _fileExists(registeredPath)
                    && IsApproved(_registry.GetStartupApprovedValue(ValueName));
            }
        }

        public void Enable()
        {
            _registry.SetRunValue(ValueName, Quote(_exePath));
            if (!IsApproved(_registry.GetStartupApprovedValue(ValueName)))
            {
                _registry.SetStartupApprovedValue(ValueName, (byte[])ApprovedEnabled.Clone());
                Trace.TraceInformation("Startup: re-enabled the startup entry that was disabled in Task Manager");
            }
            Trace.TraceInformation("Startup: enabled start with Windows for {0}", _exePath);
        }

        public void Disable()
        {
            _registry.DeleteRunValue(ValueName);
            _registry.DeleteStartupApprovedValue(ValueName);
            Trace.TraceInformation("Startup: disabled start with Windows");
        }

        public bool UpdatePathIfMoved()
        {
            var data = _registry.GetRunValue(ValueName);
            if (data == null)
            {
                return false;
            }

            var registeredPath = ParseExePath(data);
            if (string.Equals(registeredPath, _exePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _registry.SetRunValue(ValueName, Quote(_exePath));
            Trace.TraceInformation("Startup: updated the startup entry from {0} to {1}", data, Quote(_exePath));
            return true;
        }

        /// <summary>
        /// Quotes a path for the Run key, so paths with spaces are not split into exe and arguments
        /// </summary>
        internal static string Quote(string path)
        {
            return "\"" + path + "\"";
        }

        /// <summary>
        /// Extracts the exe path from Run value data: the quoted part if quoted, else the whole (trimmed) data
        /// </summary>
        /// <returns>The path, else null if the data is null or empty</returns>
        internal static string ParseExePath(string data)
        {
            if (data == null)
            {
                return null;
            }

            var trimmed = data.Trim();
            if (trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                var end = trimmed.IndexOf('"', 1);
                trimmed = end < 0 ? trimmed.Substring(1) : trimmed.Substring(1, end - 1);
            }
            return trimmed.Length == 0 ? null : trimmed;
        }

        /// <summary>
        /// Interprets Task Manager's StartupApproved flag: absent means enabled; otherwise the first byte is
        /// 0x02/0x06 when enabled and 0x03/0x07 when disabled. Conservatively, any odd first byte counts as
        /// disabled (bit 0 is the disabled bit); an empty value carries no flag and counts as enabled.
        /// </summary>
        internal static bool IsApproved(byte[] startupApproved)
        {
            if (startupApproved == null || startupApproved.Length == 0)
            {
                return true;
            }
            return (startupApproved[0] & 0x01) == 0;
        }
    }
}
