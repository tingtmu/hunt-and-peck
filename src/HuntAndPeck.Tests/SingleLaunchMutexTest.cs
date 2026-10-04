using System;
using System.Threading;
using Xunit;

namespace HuntAndPeck.Tests
{
    public class SingleLaunchMutexTest
    {
        [Fact]
        public void MutexName_IsSessionLocal()
        {
            Assert.StartsWith(@"Local\", SingleLaunchMutex.MutexName);
        }

        [Fact]
        public void SecondInstance_IsAlreadyRunning_UntilFirstDisposed()
        {
            var name = @"Local\HuntAndPeck-test-" + Guid.NewGuid().ToString("N");

            // Synchronous on purpose: the mutex must be released by the thread that acquired it
            using (var first = new SingleLaunchMutex(name))
            {
                Assert.False(first.AlreadyRunning);

                // Mutexes are re-entrant per thread, so the second instance must be on another thread
                Assert.True(IsRunningOnOtherThread(name));
            }

            Assert.False(IsRunningOnOtherThread(name));
        }

        private static bool IsRunningOnOtherThread(string name)
        {
            // A dedicated thread, since waiting on a Task may inline it on the current thread
            var alreadyRunning = false;
            var thread = new Thread(() =>
            {
                using (var other = new SingleLaunchMutex(name))
                {
                    alreadyRunning = other.AlreadyRunning;
                }
            });
            thread.Start();
            thread.Join();
            return alreadyRunning;
        }
    }
}
