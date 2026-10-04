using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace HuntAndPeck.Diagnostics
{
    /// <summary>
    /// Sets up the application log file. All System.Diagnostics.Trace output at Information level and
    /// above lands in %LOCALAPPDATA%\HuntAndPeck\hap.log.
    /// </summary>
    internal static class AppLog
    {
        public const string LogFileName = "hap.log";

        /// <summary>
        /// Default log location, %LOCALAPPDATA%\HuntAndPeck\hap.log
        /// </summary>
        public static string DefaultLogPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HuntAndPeck",
                LogFileName);

        /// <summary>
        /// The active log file, or null if file logging could not be set up
        /// </summary>
        public static string LogPath { get; private set; }

        /// <summary>
        /// Registers the file trace listener and writes the startup line
        /// </summary>
        /// <param name="mode">The startup mode, for the log</param>
        public static void Initialize(string mode)
        {
            var listener = TryCreateListener(DefaultLogPath);
            if (listener != null)
            {
                Trace.Listeners.Add(listener);
                LogPath = listener.LogPath;
            }

            var assembly = Assembly.GetExecutingAssembly();
            var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
            Trace.TraceInformation(
                "HuntAndPeck {0} (file {1}) starting, mode {2}, pid {3}; OS {4} ({5}), CLR {6}, {7}-bit process",
                assembly.GetName().Version,
                fileVersion,
                mode,
                Process.GetCurrentProcess().Id,
                Environment.OSVersion.VersionString,
                Environment.OSVersion.Version,
                Environment.Version,
                Environment.Is64BitProcess ? 64 : 32);
        }

        private static RollingFileTraceListener TryCreateListener(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                // Fall back to no file log; this still reaches the debugger via the default listener
                Trace.TraceWarning("Cannot create log directory for {0}, file logging disabled: {1}", path, ex.Message);
                return null;
            }

            return new RollingFileTraceListener(path)
            {
                Name = "HapFileLog",
                Filter = new EventTypeFilter(SourceLevels.Information),
            };
        }
    }
}
