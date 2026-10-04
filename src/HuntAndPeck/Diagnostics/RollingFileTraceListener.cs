using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace HuntAndPeck.Diagnostics
{
    /// <summary>
    /// Trace listener that appends timestamped lines to a size capped log file, rolling the file to
    /// "{path}.1" (a single backup) when it would exceed the cap.
    /// </summary>
    /// <remarks>
    /// The file is opened per write with FileShare.ReadWrite | FileShare.Delete so the log can be read while
    /// the app runs, and so headless (/hint, /tray) instances can log to the same file. A session-local named
    /// mutex serializes the size check and roll across those processes. If rolling fails the file keeps
    /// growing up to <see cref="HardCapFactor"/> times the cap, after which entries are dropped.
    /// Logging must never take the app down: every failure is swallowed (see <see cref="WriteEntry"/>).
    /// </remarks>
    internal sealed class RollingFileTraceListener : TraceListener
    {
        public const long DefaultMaxBytes = 1024 * 1024;
        public const string DefaultMutexName = @"Local\HuntAndPeck-log";

        /// <summary>When rolling fails, appending continues until the file reaches this multiple of the cap</summary>
        public const int HardCapFactor = 2;

        private static readonly TimeSpan MutexWait = TimeSpan.FromSeconds(1);
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly object _lock = new object();
        private readonly string _mutexName;
        private Mutex _mutex;

        public RollingFileTraceListener(string path, long maxBytes = DefaultMaxBytes, string mutexName = DefaultMutexName)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A log file path is required", nameof(path));
            }
            if (maxBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes), "The size cap must be positive");
            }

            LogPath = path;
            BackupPath = path + ".1";
            MaxBytes = maxBytes;
            _mutexName = mutexName;
        }

        public string LogPath { get; }
        public string BackupPath { get; }
        public long MaxBytes { get; }

        public override bool IsThreadSafe => true;

        public override void Write(string message)
        {
            WriteEntry("Message", message, null);
        }

        public override void WriteLine(string message)
        {
            WriteEntry("Message", message, null);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (Filter == null || Filter.ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
            {
                WriteEntry(eventType.ToString(), message, null);
            }
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            if (Filter == null || Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
            {
                WriteEntry(eventType.ToString(), format, args);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    _mutex?.Dispose();
                    _mutex = null;
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Formats one log line
        /// </summary>
        internal static string FormatLine(DateTime timestamp, string level, int threadId, string message)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] [T{2}] {3}{4}",
                timestamp, level, threadId, message, Environment.NewLine);
        }

        private void WriteEntry(string level, string formatOrMessage, object[] args)
        {
            lock (_lock)
            {
                try
                {
                    var message = args == null || args.Length == 0
                        ? formatOrMessage
                        : string.Format(CultureInfo.InvariantCulture, formatOrMessage, args);
                    var bytes = Utf8NoBom.GetBytes(FormatLine(DateTime.Now, level, Thread.CurrentThread.ManagedThreadId, message));
                    WriteAcrossProcesses(bytes);
                }
                catch (Exception)
                {
                    // The ONLY intentional swallow in the app: if the entry can't be formatted or written (bad
                    // format string, disk full, ACLs, sharing violation, ...) it is dropped. Logging must never
                    // throw into the app, and there is nowhere else to report the failure.
                }
            }
        }

        private void WriteAcrossProcesses(byte[] bytes)
        {
            var owned = AcquireMutex();
            try
            {
                // Rolling is only safe while holding the mutex; without it just append (within the hard cap)
                AppendWithRoll(bytes, owned);
            }
            finally
            {
                if (owned)
                {
                    _mutex.ReleaseMutex();
                }
            }
        }

        private bool AcquireMutex()
        {
            if (_mutex == null)
            {
                _mutex = new Mutex(false, _mutexName);
            }

            try
            {
                return _mutex.WaitOne(MutexWait);
            }
            catch (AbandonedMutexException)
            {
                // Another instance died while logging; we own the mutex now
                return true;
            }
        }

        private void AppendWithRoll(byte[] bytes, bool mayRoll)
        {
            var stream = OpenLog();
            try
            {
                // Length is read after acquiring the mutex, so another process's roll is already visible
                if (stream.Length > 0 && stream.Length + bytes.Length > MaxBytes && mayRoll)
                {
                    stream.Dispose();
                    stream = null;
                    TryRoll();
                    stream = OpenLog();
                }

                if (stream.Length > 0 && stream.Length + bytes.Length > MaxBytes * HardCapFactor)
                {
                    // Rolling keeps failing: drop the entry rather than grow without bound
                    return;
                }
                stream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                stream?.Dispose();
            }
        }

        private FileStream OpenLog()
        {
            return new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        }

        private void TryRoll()
        {
            try
            {
                if (File.Exists(BackupPath))
                {
                    File.Delete(BackupPath);
                }
                File.Move(LogPath, BackupPath);
            }
            catch (IOException)
            {
                // E.g. the backup is open in an editor without delete sharing: keep appending to the current
                // file (up to the hard cap) and retry the roll on the next write
            }
            catch (UnauthorizedAccessException)
            {
                // Same as above, for read-only or ACL-protected files
            }
        }
    }
}
