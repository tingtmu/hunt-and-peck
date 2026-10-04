using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services;
using HuntAndPeck.ViewModels;
using Xunit;

namespace HuntAndPeck.Tests.ViewModels
{
    public class OverlayViewModelTest
    {
        [Fact]
        public void Match_InvokesOnce_AndClosesAfterInvocationCompletes()
        {
            var invocation = new TaskCompletionSource<bool>();
            var invokes = 0;
            var closes = 0;
            var session = new HintSession { Hints = new List<Hint> { new FakeHint() } };
            var vm = new OverlayViewModel(session, new HintLabelService(), hint =>
            {
                invokes++;
                return invocation.Task;
            });
            vm.CloseOverlay = () => Interlocked.Increment(ref closes);
            var label = vm.Hints[0].Label;

            vm.MatchString = label;
            vm.MatchString = label;

            Assert.Equal(1, invokes);
            Assert.Same(invocation.Task, vm.PendingInvocation);
            Assert.Equal(0, Volatile.Read(ref closes));

            invocation.SetResult(true);
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref closes) == 1, TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void Match_HungInvocation_ClosesAfterDelay()
        {
            var closes = 0;
            var session = new HintSession { Hints = new List<Hint> { new FakeHint() } };
            var vm = new OverlayViewModel(session, new HintLabelService(), hint => new TaskCompletionSource<bool>().Task);
            vm.CloseOverlay = () => Interlocked.Increment(ref closes);

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref closes) == 1, TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task Match_InvokeAfterOverlayCloses_ClosesBeforeInvokingOnce()
        {
            var events = new List<string>();
            var invocation = new TaskCompletionSource<bool>();
            var session = new HintSession { Hints = new List<Hint> { new InvokeAfterCloseFakeHint() } };
            var vm = new OverlayViewModel(session, new HintLabelService(), hint =>
            {
                events.Add("invoke");
                return invocation.Task;
            });
            Task pendingAtClose = null;
            vm.CloseOverlay = () =>
            {
                events.Add("close");
                pendingAtClose = vm.PendingInvocation;
            };
            var label = vm.Hints[0].Label;

            vm.MatchString = label;
            vm.MatchString = label;

            Assert.Equal(new List<string> { "close", "invoke" }, events);

            // Headless mode reads PendingInvocation while the overlay closes: it must already track the invocation
            Assert.Same(pendingAtClose, vm.PendingInvocation);
            Assert.False(pendingAtClose.IsCompleted);
            invocation.SetResult(true);
            Assert.Same(pendingAtClose, await Task.WhenAny(pendingAtClose, Task.Delay(TimeSpan.FromSeconds(5))));
        }

        private sealed class InvokeAfterCloseFakeHint : Hint
        {
            public InvokeAfterCloseFakeHint()
                : base(IntPtr.Zero, new Rect(0, 0, 10, 10))
            {
            }

            public override bool InvokeAfterOverlayCloses => true;

            public override void Invoke()
            {
            }
        }

        private sealed class FakeHint : Hint
        {
            public FakeHint()
                : base(IntPtr.Zero, new Rect(0, 0, 10, 10))
            {
            }

            public override void Invoke()
            {
            }
        }
    }
}
