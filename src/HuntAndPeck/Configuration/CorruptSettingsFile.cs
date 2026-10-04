using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Locates the settings file behind a configuration error, decides whether it may be set aside and names
    /// its backup
    /// </summary>
    internal static class CorruptSettingsFile
    {
        public const string UserConfigFileName = "user.config";

        /// <summary>
        /// Finds the file that failed to load: the first non-empty <see cref="ConfigurationException.Filename"/>
        /// in the exception or its inner exceptions (the settings provider wraps the user.config error)
        /// </summary>
        /// <returns>The file path, else null</returns>
        public static string FindPath(Exception exception)
        {
            for (var ex = exception; ex != null; ex = ex.InnerException)
            {
                var configError = ex as ConfigurationException;
                if (!string.IsNullOrEmpty(configError?.Filename))
                {
                    return configError.Filename;
                }
            }
            return null;
        }

        /// <summary>
        /// Whether the file may be set aside: only a user.config inside one of the allowed folders, never
        /// hap.exe.config, machine.config or anything else
        /// </summary>
        /// <param name="path">The damaged file</param>
        /// <param name="allowedRoots">Folders the file must be inside (%LOCALAPPDATA% and %APPDATA%)</param>
        public static bool IsUserSettingsFile(string path, params string[] allowedRoots)
        {
            string fullPath;
            if (!TryGetFullPath(path, out fullPath)
                || !string.Equals(Path.GetFileName(fullPath), UserConfigFileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var root in allowedRoots)
            {
                string fullRoot;
                if (TryGetFullPath(root, out fullRoot)
                    && fullPath.StartsWith(WithTrailingSeparator(fullRoot), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Name the corrupt file is renamed to, e.g. "user.config.corrupt-20261004-101500"
        /// </summary>
        public static string BackupPath(string path, DateTime timestamp)
        {
            return path + ".corrupt-" + timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }

        private static bool TryGetFullPath(string path, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            try
            {
                // Resolves ".." segments, so "...\Local\..\Evil\user.config" can't pass the prefix check
                fullPath = Path.GetFullPath(path);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is SecurityException)
            {
                Trace.TraceWarning("Settings: invalid path \"{0}\": {1}", path, ex.Message);
                return false;
            }
        }

        private static string WithTrailingSeparator(string path)
        {
            return path.EndsWith("\\", StringComparison.Ordinal) ? path : path + "\\";
        }
    }
}
