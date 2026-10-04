using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HuntAndPeck.Services.Uia
{
    /// <summary>
    /// Owns one long-lived background MTA thread and runs queued work on it in order.
    /// </summary>
    /// <remarks>
    /// UI Automation recommends clients not call UIA from the UI thread of a window (cross-process calls
    /// can re-enter or deadlock), so all UIA objects are created and used on this thread instead.
    /// The thread is a background thread so a hung call never blocks app exit.
    /// </remarks>
    internal sealed class UiaWorker : IDisposable
    {
        private readonly BlockingCollection<WorkItem> _queue = new BlockingCollection<WorkItem>();
        private readonly Thread _thread;
        private volatile bool _disposed;

        public UiaWorker(string name)
        {
            _thread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = name,
            };
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }

        public int ManagedThreadId => _thread.ManagedThreadId;

        /// <summary>
        /// True while the worker thread exists (after disposal: until a hung call returns)
        /// </summary>
        public bool IsAlive => _thread.IsAlive;

        public bool IsDisposed => _disposed;

        /// <summary>
        /// Queues work on the worker thread
        /// </summary>
        /// <returns>
        /// A task completing with the work's result or exception; cancelled if the worker is disposed before
        /// the work starts; faulted with <see cref="ObjectDisposedException"/> if it was already disposed.
        /// Continuations run asynchronously, never inline on the worker thread.
        /// </returns>
        public Task<T> Run<T>(Func<T> work)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var item = new WorkItem(() => Execute(work, tcs), () => tcs.TrySetCanceled());
            try
            {
                _queue.Add(item);
            }
            catch (InvalidOperationException)
            {
                // Adding was completed by Dispose
                tcs.TrySetException(new ObjectDisposedException(nameof(UiaWorker)));
            }
            return tcs.Task;
        }

        /// <summary>
        /// Stops accepting work and immediately cancels work that is queued but not started. A call that is
        /// currently running (e.g. hung in a cross-process call) is left to finish on its own; the thread
        /// then exits.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            _queue.CompleteAdding();

            WorkItem item;
            while (_queue.TryTake(out item))
            {
                item.Cancel();
            }
        }

        private static void Execute<T>(Func<T> work, TaskCompletionSource<T> tcs)
        {
            try
            {
                tcs.TrySetResult(work());
            }
            catch (Exception ex)
            {
                // Not swallowed: the exception is propagated to whoever awaits the task
                tcs.TrySetException(ex);
            }
        }

        private void ProcessQueue()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                if (_disposed)
                {
                    item.Cancel();
                }
                else
                {
                    item.Execute();
                }
            }
        }

        private sealed class WorkItem
        {
            public WorkItem(Action execute, Action cancel)
            {
                Execute = execute;
                Cancel = cancel;
            }

            public Action Execute { get; }
            public Action Cancel { get; }
        }
    }
}
