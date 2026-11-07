using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HuntAndPeck.Tests.Services;
using HuntAndPeck.Updates;
using Xunit;

namespace HuntAndPeck.Tests.Updates
{
    public class UpdateCheckerTest
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        private static readonly Version Current = new Version(1, 10, 1, 0);
        private const string PageUrl = "https://github.com/tingtmu/hunt-and-peck/releases/tag/v1.11.0";

        private readonly FakeReleaseClient _client = new FakeReleaseClient { Release = new ReleaseInfo("v1.11.0", PageUrl) };
        private readonly FakeState _state = new FakeState();
        private readonly FakeClock _clock = new FakeClock();
        private readonly List<Notification> _notifications = new List<Notification>();
        private readonly List<string> _opened = new List<string>();
        private bool _automaticEnabled = true;
        private bool _isScoop;

        [Fact]
        public async Task Automatic_WaitsThirtySecondsFirst()
        {
            await Create().RunAutomaticAsync();

            Assert.Equal(new[] { TimeSpan.FromSeconds(30) }, _clock.Delays);
        }

        [Fact]
        public async Task Automatic_NewVersion_NotifiesAndRemembersTheCheck()
        {
            await Create().RunAutomaticAsync();

            var notification = Assert.Single(_notifications);
            Assert.Equal("HuntAndPeck 1.11.0 is available. Click here to open the release page.", notification.Text);
            Assert.Equal(Now, _state.LastCheckUtc);
        }

        [Fact]
        public async Task Automatic_SettingOff_DoesNothing()
        {
            _automaticEnabled = false;

            await Create().RunAutomaticAsync();

            Assert.Equal(0, _client.Calls);
            Assert.Empty(_notifications);
            Assert.Null(_state.LastCheckUtc);
        }

        [Fact]
        public async Task Automatic_SettingIsReadAfterTheDelay()
        {
            _clock.OnAdvanced = now => _automaticEnabled = false;

            await Create().RunAutomaticAsync();

            Assert.Equal(0, _client.Calls);
        }

        [Fact]
        public async Task Automatic_CheckedLessThanADayAgo_DoesNothing()
        {
            _state.LastCheckUtc = Now.AddHours(-23);

            await Create().RunAutomaticAsync();

            Assert.Equal(0, _client.Calls);
            Assert.Empty(_notifications);
        }

        [Fact]
        public async Task Automatic_CheckedOverADayAgo_Checks()
        {
            _state.LastCheckUtc = Now.AddHours(-25);

            await Create().RunAutomaticAsync();

            Assert.Equal(1, _client.Calls);
            Assert.Equal(Now, _state.LastCheckUtc);
        }

        [Fact]
        public async Task Automatic_UpToDate_SaysNothingButRemembersTheCheck()
        {
            _client.Release = new ReleaseInfo("v1.10.1", PageUrl);

            await Create().RunAutomaticAsync();

            Assert.Empty(_notifications);
            Assert.Equal(Now, _state.LastCheckUtc);
        }

        [Fact]
        public async Task Automatic_NetworkError_SaysNothingAndChecksAgainNextTime()
        {
            _client.Error = new IOException("no network");

            await Create().RunAutomaticAsync();

            Assert.Empty(_notifications);
            Assert.Null(_state.LastCheckUtc);
        }

        [Fact]
        public async Task Automatic_InvalidTag_SaysNothing()
        {
            _client.Release = new ReleaseInfo("nightly", PageUrl);

            await Create().RunAutomaticAsync();

            Assert.Empty(_notifications);
        }

        [Fact]
        public async Task Automatic_NullRelease_DoesNotThrow()
        {
            _client.Release = null;

            await Create().RunAutomaticAsync();

            Assert.Empty(_notifications);
        }

        [Fact]
        public async Task Automatic_SavingTheCheckTimeFails_StillNotifies()
        {
            _state.SaveError = new InvalidOperationException("disk full");

            await Create().RunAutomaticAsync();

            Assert.Single(_notifications);
        }

        [Fact]
        public async Task Notification_Click_OpensTheReleasePage()
        {
            await Create().RunAutomaticAsync();

            Assert.Empty(_opened);
            _notifications[0].OnClick();

            Assert.Equal(new[] { PageUrl }, _opened);
        }

        [Fact]
        public async Task Notification_Scoop_TellsToRunScoopUpdateAndOpensNothing()
        {
            _isScoop = true;

            await Create().RunAutomaticAsync();

            var notification = Assert.Single(_notifications);
            Assert.Equal("HuntAndPeck 1.11.0 is available. Run \"scoop update huntandpeck\" to update.", notification.Text);
            Assert.Null(notification.OnClick);
        }

        [Fact]
        public async Task Manual_NewVersion_Notifies_EvenIfCheckedJustNow()
        {
            _state.LastCheckUtc = Now;
            _automaticEnabled = false;

            await Create().CheckManuallyAsync();

            Assert.Contains("1.11.0 is available", Assert.Single(_notifications).Text);
            Assert.Empty(_clock.Delays);
        }

        [Fact]
        public async Task Manual_UpToDate_Says()
        {
            _client.Release = new ReleaseInfo("1.10.1", PageUrl);

            await Create().CheckManuallyAsync();

            var notification = Assert.Single(_notifications);
            Assert.Equal("HuntAndPeck 1.10.1 is up to date.", notification.Text);
            Assert.Null(notification.OnClick);
        }

        [Fact]
        public async Task Manual_NetworkError_SaysCouldntCheck()
        {
            _client.Error = new TaskCanceledException("timeout");

            await Create().CheckManuallyAsync();

            Assert.Equal(UpdateMessages.CheckFailed, Assert.Single(_notifications).Text);
        }

        [Fact]
        public async Task Manual_InvalidTag_SaysCouldntCheck()
        {
            _client.Release = new ReleaseInfo("v1.12.0-rc1", PageUrl);

            await Create().CheckManuallyAsync();

            Assert.Equal(UpdateMessages.CheckFailed, Assert.Single(_notifications).Text);
        }

        [Fact]
        public async Task Manual_WhileChecking_SaysSoWithoutAnotherRequest()
        {
            var gate = new TaskCompletionSource<ReleaseInfo>();
            _client.Pending = gate.Task;
            var checker = Create();

            var first = checker.CheckManuallyAsync();
            await checker.CheckManuallyAsync();
            gate.SetResult(_client.Release);
            await first;

            Assert.Equal(1, _client.Calls);
            Assert.Equal(UpdateMessages.AlreadyChecking, _notifications[0].Text);
            Assert.Contains("is available", _notifications[1].Text);
        }

        [Fact]
        public async Task Manual_AfterAFailedCheck_CanCheckAgain()
        {
            _client.Error = new IOException("no network");
            var checker = Create();
            await checker.CheckManuallyAsync();

            _client.Error = null;
            await checker.CheckManuallyAsync();

            Assert.Equal(2, _client.Calls);
            Assert.Contains("is available", _notifications[1].Text);
        }

        private UpdateChecker Create()
        {
            return new UpdateChecker(
                _client, _state, () => _automaticEnabled, Current, _isScoop, () => Now, _clock.Delay,
                (text, onClick) => _notifications.Add(new Notification(text, onClick)), _opened.Add);
        }

        private sealed class Notification
        {
            public Notification(string text, Action onClick)
            {
                Text = text;
                OnClick = onClick;
            }

            public string Text { get; }
            public Action OnClick { get; }
        }

        private sealed class FakeReleaseClient : IReleaseClient
        {
            public ReleaseInfo Release { get; set; }
            public Exception Error { get; set; }

            /// <summary>If set, the call completes with this task instead of at once</summary>
            public Task<ReleaseInfo> Pending { get; set; }

            public int Calls { get; private set; }

            public Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellation)
            {
                ++Calls;
                if (Error != null)
                {
                    return Task.FromException<ReleaseInfo>(Error);
                }
                return Pending ?? Task.FromResult(Release);
            }
        }

        private sealed class FakeState : IUpdateCheckState
        {
            private DateTime? _lastCheckUtc;

            public Exception SaveError { get; set; }

            public DateTime? LastCheckUtc
            {
                get { return _lastCheckUtc; }
                set
                {
                    if (SaveError != null)
                    {
                        throw SaveError;
                    }
                    _lastCheckUtc = value;
                }
            }
        }
    }
}
