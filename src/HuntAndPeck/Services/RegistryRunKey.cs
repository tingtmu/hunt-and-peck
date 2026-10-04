using System.Diagnostics;
using HuntAndPeck.Services.Interfaces;
using Microsoft.Win32;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// <see cref="IRegistryRunKey"/> over HKEY_CURRENT_USER (per-user, no elevation needed)
    /// </summary>
    internal sealed class RegistryRunKey : IRegistryRunKey
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        public string GetRunValue(string name)
        {
            var value = GetValue(RunKeyPath, name);
            if (value != null && !(value is string))
            {
                Trace.TraceWarning("Startup: Run value {0} is not a string ({1}); ignoring it", name, value.GetType().Name);
                return null;
            }
            return (string)value;
        }

        public void SetRunValue(string name, string data)
        {
            SetValue(RunKeyPath, name, data, RegistryValueKind.String);
        }

        public void DeleteRunValue(string name)
        {
            DeleteValue(RunKeyPath, name);
        }

        public byte[] GetStartupApprovedValue(string name)
        {
            var value = GetValue(StartupApprovedKeyPath, name);
            if (value != null && !(value is byte[]))
            {
                Trace.TraceWarning("Startup: StartupApproved value {0} is not binary ({1}); ignoring it", name, value.GetType().Name);
                return null;
            }
            return (byte[])value;
        }

        public void SetStartupApprovedValue(string name, byte[] data)
        {
            SetValue(StartupApprovedKeyPath, name, data, RegistryValueKind.Binary);
        }

        public void DeleteStartupApprovedValue(string name)
        {
            DeleteValue(StartupApprovedKeyPath, name);
        }

        private static object GetValue(string keyPath, string name)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, false))
            {
                return key?.GetValue(name);
            }
        }

        private static void SetValue(string keyPath, string name, object data, RegistryValueKind kind)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath, true))
            {
                key.SetValue(name, data, kind);
            }
        }

        private static void DeleteValue(string keyPath, string name)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
            {
                key?.DeleteValue(name, false);
            }
        }
    }
}
