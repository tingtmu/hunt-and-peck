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
            Assert.False(vm.PendingInvocation.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref closes));

            invocation.SetResult(true);
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref closes) == 1, TimeSpan.FromSeconds(5)));
            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void Match_HungInvocation_ClosesAfterDelay()
        {
            var closes = 0;
            var invocation = new TaskCompletionSource<bool>();
            var session = new HintSession { Hints = new List<Hint> { new FakeHint() } };
            var vm = new OverlayViewModel(session, new HintLabelService(), hint => invocation.Task);
            vm.CloseOverlay = () => Interlocked.Increment(ref closes);

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref closes) == 1, TimeSpan.FromSeconds(5)));
            Assert.False(vm.PendingInvocation.IsCompleted);

            // The real invoker always completes (it times out); the async void InvokeAndClose awaits it after
            // closing, and xUnit waits for outstanding async void operations before ending the test
            invocation.SetResult(true);
            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
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

        [Fact]
        public void Match_FailedInvocation_ClicksOnceAfterClose_AndPendingCoversTheClick()
        {
            var click = new ClickFakeHint();
            var original = new FallbackFakeHint("original", click);
            var originalResult = new TaskCompletionSource<bool>();
            var clickResult = new TaskCompletionSource<bool>();
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => hint == original ? originalResult.Task : clickResult.Task);
            var label = vm.Hints[0].Label;

            vm.MatchString = label;
            vm.MatchString = label;

            Assert.Equal("invoke:original", recorder.Snapshot()[0]);
            Assert.False(vm.PendingInvocation.IsCompleted);

            originalResult.SetResult(false);
            Assert.True(SpinWait.SpinUntil(() => recorder.Count == 3, TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "invoke:original", "close", "invoke:click" }, recorder.Snapshot());
            Assert.False(vm.PendingInvocation.IsCompleted);

            clickResult.SetResult(true);
            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(3, recorder.Count);
        }

        [Fact]
        public void Match_SucceededInvocation_DoesNotClick()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(true));

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "invoke:original", "close" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_FailedInvocationAndFailedClick_InvokesTwiceWithoutLooping()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(false));

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "invoke:original", "close", "invoke:click" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_FailedInvocationWithoutFallback_InvokesOnce()
        {
            var original = new FallbackFakeHint("original", null);
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(false));

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "invoke:original", "close" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_ForceClick_InvokesOnlyTheClickAfterClose()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(true));
            vm.ForceClick = true;

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "close", "invoke:click" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_ForceClickWithoutFallback_InvokesTheOriginalHint()
        {
            var original = new FallbackFakeHint("original", null);
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(true));
            vm.ForceClick = true;

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "invoke:original", "close" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_FailedInvocationOfHintThatMustNotClickOnFailure_DoesNotClick()
        {
            // E.g. a focus hint on a slider: clicking it would change the value
            var original = new FallbackFakeHint("original", new ClickFakeHint(), clicksOnFailure: false);
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(false));

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "invoke:original", "close" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_ForceClickOnHintThatMustNotClickOnFailure_StillClicks()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint(), clicksOnFailure: false);
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(true));
            vm.ForceClick = true;

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "close", "invoke:click" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_FailureAfterDeadline_DoesNotClick()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var recorder = new EventRecorder();
            var late = OverlayViewModel.ClickFallbackDeadline + TimeSpan.FromMilliseconds(300);
            var vm = CreateViewModel(original, recorder, hint => Task.Delay(late).ContinueWith(_ => false));

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new List<string> { "invoke:original", "close" }, recorder.Snapshot());
        }

        private static OverlayViewModel CreateViewModel(Hint hint, EventRecorder recorder, Func<Hint, Task<bool>> result)
        {
            var session = new HintSession { Hints = new List<Hint> { hint } };
            var vm = new OverlayViewModel(session, new HintLabelService(), h =>
            {
                recorder.Add("invoke:" + ((INamedFakeHint)h).Name);
                return result(h);
            });
            vm.CloseOverlay = () => recorder.Add("close");
            return vm;
        }

        /// <summary>Thread-safe event log: continuations run on the thread pool without a SynchronizationContext</summary>
        private sealed class EventRecorder
        {
            private readonly object _lock = new object();
            private readonly List<string> _events = new List<string>();

            public int Count
            {
                get
                {
                    lock (_lock)
                    {
                        return _events.Count;
                    }
                }
            }

            public void Add(string e)
            {
                lock (_lock)
                {
                    _events.Add(e);
                }
            }

            public List<string> Snapshot()
            {
                lock (_lock)
                {
                    return new List<string>(_events);
                }
            }
        }

        private interface INamedFakeHint
        {
            string Name { get; }
        }

        /// <summary>A pattern-action hint whose click hint is the given hint (null for none)</summary>
        private sealed class FallbackFakeHint : Hint, INamedFakeHint
        {
            private readonly Hint _clickHint;
            private readonly bool _clicksOnFailure;

            public FallbackFakeHint(string name, Hint clickHint, bool clicksOnFailure = true)
                : base(IntPtr.Zero, new Rect(0, 0, 10, 10))
            {
                Name = name;
                _clickHint = clickHint;
                _clicksOnFailure = clicksOnFailure;
            }

            public string Name { get; }

            public override Hint CreateClickHint() => _clickHint;

            public override bool ClicksOnFailure => _clicksOnFailure;

            public override void Invoke()
            {
            }
        }

        /// <summary>Like UiAutomationClickHint: runs after the overlay closed, no further fallback</summary>
        private sealed class ClickFakeHint : Hint, INamedFakeHint
        {
            public ClickFakeHint()
                : base(IntPtr.Zero, new Rect(0, 0, 10, 10))
            {
            }

            public string Name => "click";

            public override bool InvokeAfterOverlayCloses => true;

            public override void Invoke()
            {
            }
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
