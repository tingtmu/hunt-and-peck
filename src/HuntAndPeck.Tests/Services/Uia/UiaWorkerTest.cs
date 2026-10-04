using System;
using System.Threading;
using System.Threading.Tasks;
using HuntAndPeck.Services.Uia;
using Xunit;

namespace HuntAndPeck.Tests.Services.Uia
{
    public class UiaWorkerTest
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task Run_ExecutesOnDedicatedBackgroundMtaThread()
        {
            using (var worker = new UiaWorker("test worker"))
            {
                var info = await worker.Run(() => Tuple.Create(
                    Thread.CurrentThread.GetApartmentState(),
                    Thread.CurrentThread.IsBackground,
                    Thread.CurrentThread.ManagedThreadId,
                    Thread.CurrentThread.Name));

                Assert.Equal(ApartmentState.MTA, info.Item1);
                Assert.True(info.Item2);
                Assert.Equal(worker.ManagedThreadId, info.Item3);
                Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, info.Item3);
                Assert.Equal("test worker", info.Item4);
            }
        }

        [Fact]
        public async Task Run_AllWorkUsesTheSameThread()
        {
            using (var worker = new UiaWorker("test worker"))
            {
                var first = await worker.Run(() => Thread.CurrentThread.ManagedThreadId);
                var second = await worker.Run(() => Thread.CurrentThread.ManagedThreadId);

                Assert.Equal(first, second);
            }
        }

        [Fact]
        public async Task Run_PropagatesExceptionToTask()
        {
            using (var worker = new UiaWorker("test worker"))
            {
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => worker.Run<int>(() => throw new InvalidOperationException("boom")));

                Assert.Equal("boom", ex.Message);
                // The worker survives a failed item
                Assert.Equal(42, await worker.Run(() => 42));
            }
        }

        [Fact]
        public async Task Dispose_CancelsQueuedWork_AndRejectsNewWork()
        {
            var worker = new UiaWorker("test worker");
            using (var started = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var blocking = worker.Run(() =>
                {
                    started.Set();
                    return release.Wait(TestTimeout);
                });
                var queued = worker.Run(() => 1);
                Assert.True(started.Wait(TestTimeout));

                worker.Dispose();

                // Cancelled immediately, not when the running (hung) item returns
                Assert.True(queued.IsCanceled);
                Assert.True(worker.IsAlive);
                release.Set();

                Assert.True(await blocking);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.Run(() => 2));
            }
        }
    }
}
