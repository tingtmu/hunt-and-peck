using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HuntAndPeck.Diagnostics;
using Xunit;

namespace HuntAndPeck.Tests.Diagnostics
{
    public class RollingFileTraceListenerTest : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "hap-log-test-" + Guid.NewGuid().ToString("N"));
        private readonly string _logPath;

        public RollingFileTraceListenerTest()
        {
            Directory.CreateDirectory(_directory);
            _logPath = Path.Combine(_directory, "hap.log");
        }

        public void Dispose()
        {
            Directory.Delete(_directory, true);
        }

        [Fact]
        public void WriteLine_AppendsTimestampedLines()
        {
            var listener = new RollingFileTraceListener(_logPath);

            listener.WriteLine("first");
            listener.TraceEvent(null, "src", TraceEventType.Warning, 0, "second {0}", 2);

            var lines = File.ReadAllLines(_logPath);
            Assert.Equal(2, lines.Length);
            Assert.EndsWith("[Message] [T" + System.Threading.Thread.CurrentThread.ManagedThreadId + "] first", lines[0]);
            Assert.Contains("[Warning]", lines[1]);
            Assert.EndsWith("second 2", lines[1]);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} ", lines[0]);
        }

        [Fact]
        public void Write_RollsToSingleBackupWhenCapExceeded()
        {
            const long maxBytes = 300;
            var listener = new RollingFileTraceListener(_logPath, maxBytes);

            for (var i = 0; i < 50; ++i)
            {
                listener.WriteLine("line " + i.ToString("D3") + new string('x', 20));
            }

            Assert.True(File.Exists(listener.BackupPath));
            Assert.InRange(new FileInfo(_logPath).Length, 1, maxBytes);
            Assert.InRange(new FileInfo(listener.BackupPath).Length, 1, maxBytes);
            Assert.Equal(new[] { "hap.log", "hap.log.1" }, Directory.GetFiles(_directory).Select(Path.GetFileName).OrderBy(x => x));
            Assert.EndsWith("line 049" + new string('x', 20), File.ReadAllLines(_logPath).Last());
        }

        [Fact]
        public void Write_SingleEntryLargerThanCap_IsStillWritten()
        {
            var listener = new RollingFileTraceListener(_logPath, 10);

            listener.WriteLine(new string('y', 100));

            Assert.Contains(new string('y', 100), File.ReadAllText(_logPath));
            Assert.False(File.Exists(listener.BackupPath));
        }

        [Fact]
        public void Write_UnwritablePath_DoesNotThrow()
        {
            var listener = new RollingFileTraceListener(Path.Combine(_directory, "missing-dir", "hap.log"));

            listener.WriteLine("dropped");
            listener.TraceEvent(null, "src", TraceEventType.Error, 0, "dropped too");

            Assert.False(File.Exists(listener.LogPath));
        }

        [Fact]
        public void Write_RollFails_KeepsAppendingUpToHardCap()
        {
            const long maxBytes = 300;
            var listener = new RollingFileTraceListener(_logPath, maxBytes);
            File.WriteAllText(listener.BackupPath, "old backup");

            // Holding the backup open without delete sharing makes the roll fail
            using (new FileStream(listener.BackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                for (var i = 0; i < 50; ++i)
                {
                    listener.WriteLine("line " + i.ToString("D3") + new string('x', 20));
                }
            }

            var length = new FileInfo(_logPath).Length;
            Assert.InRange(length, maxBytes + 1, maxBytes * RollingFileTraceListener.HardCapFactor);
            Assert.Equal("old backup", File.ReadAllText(listener.BackupPath));

            // Once the backup is free again, the next write rolls
            listener.WriteLine("after");
            Assert.EndsWith("after", File.ReadAllLines(_logPath).Single());
        }

        [Fact]
        public void TraceEvent_BadFormatString_DoesNotThrow()
        {
            var listener = new RollingFileTraceListener(_logPath);

            listener.TraceEvent(null, "src", TraceEventType.Warning, 0, "broken {1}", "only one arg");
            listener.WriteLine("still works");

            Assert.EndsWith("still works", File.ReadAllLines(_logPath).Single());
        }

        [Fact]
        public void TraceEvent_BelowFilterLevel_IsNotWritten()
        {
            var listener = new RollingFileTraceListener(_logPath) { Filter = new EventTypeFilter(SourceLevels.Warning) };

            listener.TraceEvent(null, "src", TraceEventType.Information, 0, "info");
            listener.TraceEvent(null, "src", TraceEventType.Warning, 0, "warn");

            var lines = File.ReadAllLines(_logPath);
            Assert.Single(lines);
            Assert.EndsWith("warn", lines[0]);
        }

        [Fact]
        public void Write_ConcurrentWriters_LoseNoLines()
        {
            var listener = new RollingFileTraceListener(_logPath);

            Parallel.For(0, 200, i => listener.WriteLine("entry " + i));

            Assert.Equal(200, File.ReadAllLines(_logPath).Length);
        }
    }
}
