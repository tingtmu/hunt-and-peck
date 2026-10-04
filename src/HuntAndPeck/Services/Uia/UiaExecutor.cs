using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HuntAndPeck.Diagnostics;

namespace HuntAndPeck.Services.Uia
{
    /// <summary>
    /// Runs UI Automation work on a <see cref="UiaWorker"/> with an overall timeout.
    /// </summary>
    /// <remarks>
    /// If work times out (a hung target app that the UIA connection/transaction timeouts did not catch),
    /// the worker is abandoned: it is disposed so its queued work is cancelled, and its thread (a background
    /// thread) is left to unblock or die with the process. The next call gets a fresh worker, so one hung
    /// app can't wedge every later hint session. At most <see cref="MaxAbandonedWorkers"/> abandoned threads
    /// may still be alive; beyond that calls fail fast instead of leaking more threads.
    /// </remarks>
    internal sealed class UiaExecutor : IDisposable
    {
        public const int MaxAbandonedWorkers = 4;

        private readonly string _threadName;
        private readonly Action<string> _notifyUser;
        private readonly NotificationThrottle _notifyThrottle = new NotificationThrottle(TimeSpan.FromMinutes(10));
        private readonly List<UiaWorker> _abandoned = new List<UiaWorker>();
        private readonly object _lock = new object();
        private UiaWorker _worker;
        private bool _disposed;

        /// <param name="threadName">Name for worker threads</param>
        /// <param name="notifyUser">Optional user notification (e.g. tray balloon) when the worker limit is hit</param>
        public UiaExecutor(string threadName, Action<string> notifyUser = null)
        {
            _threadName = threadName;
            _notifyUser = notifyUser;
        }

        /// <summary>
        /// Number of abandoned workers whose thread is still alive (stuck in a hung call)
        /// </summary>
        public int LiveAbandonedWorkers
        {
            get
            {
                lock (_lock)
                {
                    _abandoned.RemoveAll(x => !x.IsAlive);
                    return _abandoned.Count;
                }
            }
        }

        /// <summary>
        /// Runs the work on the worker thread
        /// </summary>
        /// <remarks>
        /// The timeout clock starts when the work is queued, not when it starts running, so time spent
        /// queued behind earlier work counts. If the worker is abandoned (by another call timing out) while
        /// this work is still queued, the work is retried once on a fresh worker with a fresh timeout.
        /// </remarks>
        /// <exception cref="TimeoutException">
        /// The work did not complete within the timeout, or too many workers are stuck (<see cref="UiaWorkerLimitException"/>)
        /// </exception>
        public async Task<T> RunAsync<T>(Func<T> work, TimeSpan timeout)
        {
            try
            {
                return await RunOnceAsync(work, timeout).ConfigureAwait(false);
            }
            catch (WorkerAbandonedException)
            {
                Trace.TraceInformation("UI Automation work was queued on an abandoned worker; retrying on a fresh one");
            }

            try
            {
                return await RunOnceAsync(work, timeout).ConfigureAwait(false);
            }
            catch (WorkerAbandonedException)
            {
                throw new TimeoutException("UI Automation work could not run: its worker was abandoned twice");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                _worker?.Dispose();
                _worker = null;
            }
        }

        private async Task<T> RunOnceAsync<T>(Func<T> work, TimeSpan timeout)
        {
            var worker = GetWorker();
            var task = worker.Run(work);

            Task completed;
            using (var delayCancellation = new CancellationTokenSource())
            {
                completed = await Task.WhenAny(task, Task.Delay(timeout, delayCancellation.Token)).ConfigureAwait(false);
                delayCancellation.Cancel();
            }

            if (completed != task)
            {
                Abandon(worker);
                ObserveLateFailure(task);
                throw new TimeoutException(string.Format("UI Automation work did not complete within {0} ms", timeout.TotalMilliseconds));
            }

            // Cancelled or rejected only because the worker was disposed (abandoned) before the work started
            if (task.IsCanceled || (worker.IsDisposed && task.IsFaulted && task.Exception.InnerException is ObjectDisposedException))
            {
                ObserveLateFailure(task);
                throw new WorkerAbandonedException();
            }

            return await task.ConfigureAwait(false);
        }

        private UiaWorker GetWorker()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(UiaExecutor));
                }
                if (_worker == null)
                {
                    EnsureBelowAbandonedLimit();
                    _worker = new UiaWorker(_threadName);
                }
                return _worker;
            }
        }

        private void EnsureBelowAbandonedLimit()
        {
            _abandoned.RemoveAll(x => !x.IsAlive);
            if (_abandoned.Count < MaxAbandonedWorkers)
            {
                return;
            }

            Trace.TraceWarning("{0} UI Automation worker threads are stuck in hung apps; not starting another", _abandoned.Count);
            if (_notifyUser != null && _notifyThrottle.TryAcquire(DateTime.UtcNow))
            {
                _notifyUser("HuntAndPeck is waiting on apps that are not responding. Close or restart them, or restart HuntAndPeck.");
            }
            throw new UiaWorkerLimitException(_abandoned.Count);
        }

        private void Abandon(UiaWorker worker)
        {
            lock (_lock)
            {
                if (_worker != worker)
                {
                    // Already abandoned by a concurrent timeout
                    return;
                }
                _worker = null;
                _abandoned.Add(worker);
            }
            worker.Dispose();
            Trace.TraceWarning("UI Automation worker thread {0} timed out and was abandoned; a new one will be used", worker.ManagedThreadId);
        }

        private static void ObserveLateFailure<T>(Task<T> task)
        {
            // Nobody awaits this task any more; observe it so a late failure is logged, not unobserved
            task.ContinueWith(
                t => Trace.TraceInformation("Abandoned UI Automation work later failed: {0}", t.Exception?.GetBaseException().Message),
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private sealed class WorkerAbandonedException : Exception
        {
        }
    }

    /// <summary>
    /// Too many UIA worker threads are stuck in hung apps to start another
    /// </summary>
    internal sealed class UiaWorkerLimitException : TimeoutException
    {
        public UiaWorkerLimitException(int stuckWorkers)
            : base(string.Format("{0} UI Automation worker threads are stuck; not starting another", stuckWorkers))
        {
        }
    }
}
