using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
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
            vm.ForcedAction = MouseAction.LeftClick;

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
            vm.ForcedAction = MouseAction.LeftClick;

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
            vm.ForcedAction = MouseAction.LeftClick;

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

        [Theory]
        [InlineData(MouseAction.RightClick)]
        [InlineData(MouseAction.DoubleClick)]
        public void Match_ForcedAction_ClicksThatWay(MouseAction action)
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(true));
            vm.ForcedAction = action;

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new[] { action }, original.RequestedActions);
            Assert.Equal(new List<string> { "close", "invoke:click" }, recorder.Snapshot());
        }

        [Fact]
        public void FailedInvocation_FallsBackToALeftClick()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var vm = CreateViewModel(original, new EventRecorder(), hint => Task.FromResult(false));

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(new[] { MouseAction.LeftClick }, original.RequestedActions);
        }

        [Theory]
        [InlineData(false, false, false, null)]
        [InlineData(true, false, false, MouseAction.LeftClick)]
        [InlineData(false, true, false, MouseAction.RightClick)]
        [InlineData(true, true, false, MouseAction.DoubleClick)]
        [InlineData(false, false, true, MouseAction.Hover)]
        [InlineData(true, false, true, MouseAction.Hover)]
        [InlineData(false, true, true, MouseAction.RightClick)]
        [InlineData(true, true, true, MouseAction.DoubleClick)]
        public void ForcedActionFor_Modifiers(bool shift, bool ctrl, bool alt, MouseAction? expected)
        {
            Assert.Equal(expected, OverlayViewModel.ForcedActionFor(shift, ctrl, alt));
        }

        [Fact]
        public void Match_ForcedHoverSucceeded_SetsHovered()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(true));
            vm.ForcedAction = MouseAction.Hover;

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(vm.Hovered);
            Assert.Equal(new[] { MouseAction.Hover }, original.RequestedActions);
        }

        [Fact]
        public void Match_ForcedHoverFailed_IsNotHoveredAndDoesNotClick()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var recorder = new EventRecorder();
            var vm = CreateViewModel(original, recorder, hint => Task.FromResult(false));
            vm.ForcedAction = MouseAction.Hover;

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(vm.Hovered);
            Assert.Equal(new List<string> { "close", "invoke:click" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_ForcedHoverOnHintThatCannotClick_RunsItsActionAndIsNotHovered()
        {
            var recorder = new EventRecorder();
            var vm = CreateViewModel(new ClickFakeHint(), recorder, hint => Task.FromResult(true));
            vm.ForcedAction = MouseAction.Hover;

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(vm.Hovered);
            Assert.Equal(new List<string> { "close", "invoke:click" }, recorder.Snapshot());
        }

        [Fact]
        public void Match_ForcedRightClick_IsNotHovered()
        {
            var original = new FallbackFakeHint("original", new ClickFakeHint());
            var vm = CreateViewModel(original, new EventRecorder(), hint => Task.FromResult(true));
            vm.ForcedAction = MouseAction.RightClick;

            vm.MatchString = vm.Hints[0].Label;

            Assert.True(vm.PendingInvocation.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(vm.Hovered);
        }

        [Fact]
        public void AllHintsShownAtFirst_ThenOnlyThoseMatchingTheTypedLetters()
        {
            var vm = CreateViewModelWithHints(40);
            Assert.All(vm.Hints, x => Assert.True(x.Active));

            var prefix = vm.Hints[0].Label.Substring(0, 1);
            vm.MatchString = prefix;

            Assert.All(vm.Hints, x => Assert.Equal(x.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), x.Active));
            Assert.Contains(vm.Hints, x => !x.Active);

            vm.MatchString = "";
            Assert.All(vm.Hints, x => Assert.True(x.Active));
        }

        [Fact]
        public void HasMatch_IsFalseForLettersNoLabelStartsWith()
        {
            var vm = CreateViewModelWithHints(3);

            Assert.True(vm.HasMatch(""));
            Assert.True(vm.HasMatch(vm.Hints[0].Label.ToLowerInvariant()));
            Assert.False(vm.HasMatch(vm.Hints[0].Label + "Z"));
            Assert.False(vm.HasMatch("1"));
        }

        [Fact]
        public void Labels_FollowReadingOrder()
        {
            var right = new PlacedFakeHint(100, 0);
            var left = new PlacedFakeHint(0, 0);
            var below = new PlacedFakeHint(0, 100);
            var labels = new HintLabelService().GetHintStrings(3);

            var vm = new OverlayViewModel(new HintSession { Hints = new List<Hint> { below, right, left } }, new HintLabelService(), hint => Task.FromResult(true));

            Assert.Equal(new Hint[] { left, right, below }, vm.Hints.Select(x => x.Hint));
            Assert.Equal(labels, vm.Hints.Select(x => x.Label));
        }

        private static OverlayViewModel CreateViewModelWithHints(int count)
        {
            var hints = Enumerable.Range(0, count).Select(i => (Hint)new PlacedFakeHint(i * 20, 0)).ToList();
            return new OverlayViewModel(new HintSession { Hints = hints }, new HintLabelService(), hint => Task.FromResult(true));
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

            public override Hint CreateClickHint(MouseAction action)
            {
                RequestedActions.Add(action);
                return _clickHint;
            }

            public List<MouseAction> RequestedActions { get; } = new List<MouseAction>();

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

        private sealed class PlacedFakeHint : Hint
        {
            public PlacedFakeHint(double left, double top)
                : base(IntPtr.Zero, new Rect(left, top, 10, 10))
            {
            }

            public override void Invoke()
            {
            }
        }
    }
}
