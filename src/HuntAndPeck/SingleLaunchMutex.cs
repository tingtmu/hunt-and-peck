using System;
using System.Diagnostics;
using System.Threading;

namespace HuntAndPeck
{
    /// <summary>
    /// Detects another running tray instance in the same Windows session
    /// </summary>
    /// <remarks>Dispose on the thread that created it (a mutex must be released by its owning thread)</remarks>
    public class SingleLaunchMutex : IDisposable
    {
        /// <summary>
        /// Session-local name: each signed-in user (fast user switching, RDP) gets their own instance
        /// </summary>
        public const string MutexName = @"Local\HuntAndPeck-5B5486F3-15E3-4DD5-BF05-03C3F716483E";

        private readonly bool _aquiredHandle;
        private Mutex _mutex;

        public SingleLaunchMutex()
            : this(MutexName)
        {
        }

        internal SingleLaunchMutex(string name)
        {
            _mutex = new Mutex(false, name);
            try
            {
                _aquiredHandle = _mutex.WaitOne(TimeSpan.Zero, false);
            }
            catch (AbandonedMutexException)
            {
                // This will happen if the mutex isn't disposed properly, e.g. during a crash; we now own it
                Trace.TraceWarning("Previous instance exited without releasing the single launch mutex");
                _aquiredHandle = true;
            }
        }

        public bool AlreadyRunning
        {
            get { return !_aquiredHandle; }
        }

        public void Dispose()
        {
            if (_mutex != null)
            {
                if (_aquiredHandle)
                {
                    _mutex.ReleaseMutex();
                }

                _mutex.Dispose();
                _mutex = null;
            }
        }
    }
}
