using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using HuntAndPeck.Services.Uia;

namespace HuntAndPeck.Tests.Integration
{
    /// <summary>
    /// Starts the WinForms fixture window (HuntAndPeck.UiaFixture) for the duration of a test class, and runs
    /// UI Automation work against it on the same kind of worker the app uses (<see cref="UiaExecutor"/>, MTA).
    /// </summary>
    /// <remarks>
    /// The fixture exe is copied next to the test assembly by the test project's build (see the csproj), and
    /// looked up from the assembly's original location (CodeBase), so xunit shadow copying and a changed
    /// working directory don't matter, and no repository layout or configuration name is assumed here.
    /// Test classes using it share one xunit collection: UIA calls from several worker threads at once made
    /// CreatePropertyCondition fail with E_FAIL now and then.
    /// </remarks>
    public sealed class UiaFixtureApp : IDisposable
    {
        /// <summary>One collection for all fixture tests: they run one after the other, not in parallel (see class remarks)</summary>
        public const string CollectionName = "UIA fixture";

        private const string ExeName = "HuntAndPeck.UiaFixture.exe";
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);
        private readonly Process _process;

        public UiaFixtureApp()
        {
            Executor = new UiaExecutor("UIA fixture test worker");
            _process = Process.Start(FindExe());
            try
            {
                Handle = WaitForMainWindow(_process);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal UiaExecutor Executor { get; }

        public IntPtr Handle { get; }

        public void Dispose()
        {
            Executor.Dispose();
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(5000);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
            {
                // Already gone
            }
            _process.Dispose();
        }

        /// <summary>
        /// The window handle exists before the form has laid out and shown its controls; wait until UI Automation
        /// sees the button, so tests never scan a half-built window
        /// </summary>
        private void WaitUntilControlsAreExposed()
        {
            if (!FixtureUia.WaitForNameAsync(this, "Press me").GetAwaiter().GetResult())
            {
                throw new TimeoutException("UI Automation fixture app did not expose its controls");
            }
        }

        private static string FindExe()
        {
            var testDirectory = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            var path = Path.Combine(testDirectory, ExeName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("UI Automation fixture app not found; build the whole solution", path);
            }
            return path;
        }

        private static IntPtr WaitForMainWindow(Process process)
        {
            var deadline = DateTime.UtcNow + StartTimeout;
            process.WaitForInputIdle((int)StartTimeout.TotalMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                process.Refresh();
                if (process.HasExited)
                {
                    throw new InvalidOperationException("UI Automation fixture app exited with code " + process.ExitCode);
                }
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }
                Thread.Sleep(50);
            }
            throw new TimeoutException("UI Automation fixture app showed no window within " + StartTimeout);
        }
    }
}


