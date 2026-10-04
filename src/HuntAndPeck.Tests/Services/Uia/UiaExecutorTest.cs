using System;
using System.Threading;
using System.Threading.Tasks;
using HuntAndPeck.Services.Uia;
using Xunit;

namespace HuntAndPeck.Tests.Services.Uia
{
    public class UiaExecutorTest
    {
        private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(100);

        [Fact]
        public async Task RunAsync_ReturnsResultFromMtaThread()
        {
            using (var executor = new UiaExecutor("test executor"))
            {
                var apartment = await executor.RunAsync(() => Thread.CurrentThread.GetApartmentState(), LongTimeout);

                Assert.Equal(ApartmentState.MTA, apartment);
            }
        }

        [Fact]
        public async Task RunAsync_PropagatesException()
        {
            using (var executor = new UiaExecutor("test executor"))
            {
                await Assert.ThrowsAsync<TimeoutException>(
                    () => executor.RunAsync<int>(() => throw new TimeoutException("UIA_E_TIMEOUT from target"), LongTimeout));
            }
        }

        [Fact]
        public async Task RunAsync_HungWork_TimesOutWithoutBlocking_AndNextCallUsesFreshThread()
        {
            using (var executor = new UiaExecutor("test executor"))
            using (var release = new ManualResetEventSlim())
            {
                var hungThreadId = 0;
                var started = DateTime.UtcNow;

                await Assert.ThrowsAsync<TimeoutException>(() => executor.RunAsync(() =>
                {
                    hungThreadId = Thread.CurrentThread.ManagedThreadId;
                    return release.Wait(LongTimeout);
                }, ShortTimeout));

                Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5), "timeout must not wait for the hung work");

                // The hung worker is abandoned; later work is not stuck behind it
                var nextThreadId = await executor.RunAsync(() => Thread.CurrentThread.ManagedThreadId, LongTimeout);
                Assert.NotEqual(hungThreadId, nextThreadId);

                release.Set();
            }
        }

        [Fact]
        public async Task RunAsync_AfterDispose_Throws()
        {
            var executor = new UiaExecutor("test executor");
            executor.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => executor.RunAsync(() => 1, LongTimeout));
        }

        [Fact]
        public async Task RunAsync_WorkQueuedOnAbandonedWorker_IsRetriedOnFreshWorker()
        {
            using (var executor = new UiaExecutor("test executor"))
            using (var started = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var hungThreadId = 0;
                var hung = executor.RunAsync(() =>
                {
                    hungThreadId = Thread.CurrentThread.ManagedThreadId;
                    started.Set();
                    return release.Wait(LongTimeout);
                }, ShortTimeout);
                Assert.True(started.Wait(LongTimeout));

                // Queued behind the hung work; cancelled when the hung call abandons the worker
                var queued = executor.RunAsync(() => Thread.CurrentThread.ManagedThreadId, LongTimeout);

                await Assert.ThrowsAsync<TimeoutException>(() => hung);
                var queuedThreadId = await queued;

                Assert.NotEqual(hungThreadId, queuedThreadId);
                release.Set();
            }
        }

        [Fact]
        public async Task RunAsync_TooManyStuckWorkers_FailsFastAndNotifiesOnce()
        {
            var notifications = 0;
            using (var executor = new UiaExecutor("test executor", message => notifications++))
            using (var release = new ManualResetEventSlim())
            {
                for (var i = 0; i < UiaExecutor.MaxAbandonedWorkers; ++i)
                {
                    await Assert.ThrowsAsync<TimeoutException>(() => executor.RunAsync(() => release.Wait(LongTimeout), ShortTimeout));
                }
                Assert.Equal(UiaExecutor.MaxAbandonedWorkers, executor.LiveAbandonedWorkers);

                await Assert.ThrowsAsync<UiaWorkerLimitException>(() => executor.RunAsync(() => 1, LongTimeout));
                await Assert.ThrowsAsync<UiaWorkerLimitException>(() => executor.RunAsync(() => 1, LongTimeout));
                Assert.Equal(1, notifications);

                // Once the hung calls return, their threads exit and work runs again
                release.Set();
                var deadline = DateTime.UtcNow + LongTimeout;
                while (executor.LiveAbandonedWorkers > 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20);
                }
                Assert.Equal(7, await executor.RunAsync(() => 7, LongTimeout));
            }
        }
    }
}
