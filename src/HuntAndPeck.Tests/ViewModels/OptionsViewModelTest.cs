using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows.Forms;
using HuntAndPeck.Configuration;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services.Interfaces;
using HuntAndPeck.ViewModels;
using Xunit;

namespace HuntAndPeck.Tests.ViewModels
{
    public class OptionsViewModelTest
    {
        private readonly FakeUserSettings _settings = new FakeUserSettings();
        private readonly List<Tuple<HotKeyCombination, HotKeyCombination>> _applied = new List<Tuple<HotKeyCombination, HotKeyCombination>>();
        private readonly List<bool> _suspended = new List<bool>();
        private readonly HashSet<HotKeyCombination> _taken = new HashSet<HotKeyCombination>();
        private int _closed;

        private static readonly HotKeyCombination CtrlAltH = new HotKeyCombination(KeyModifier.Control | KeyModifier.Alt, Keys.H);

        [Fact]
        public void Constructor_ShowsCurrentSettings()
        {
            var vm = Create();

            Assert.Equal("14", vm.FontSizeText);
            Assert.Equal("SADFJKLEWCMPGH", vm.HintAlphabetText);
            Assert.Equal("Alt+;", vm.MainHotKey.DisplayText);
            Assert.Equal("Ctrl+;", vm.TaskbarHotKey.DisplayText);
        }

        [Fact]
        public void EditsWithoutSave_PersistNothing()
        {
            var vm = Create();

            vm.FontSizeText = "20";
            vm.HintAlphabetText = "jk";
            vm.MainHotKey.CaptureCommand.Execute(CtrlAltH);

            Assert.Empty(_settings.Saved);
            Assert.Empty(_applied);
            Assert.Equal(0, _closed);
        }

        [Fact]
        public void Save_Valid_AppliesPersistsAndCloses()
        {
            var vm = Create();
            vm.FontSizeText = "20.5";
            vm.HintAlphabetText = "jkl";
            vm.MainHotKey.CaptureCommand.Execute(CtrlAltH);

            Assert.True(vm.Save());

            var saved = Assert.Single(_settings.Saved);
            Assert.Equal(20.5, saved.FontSize);
            Assert.Equal("JKL", saved.HintAlphabet);
            Assert.Equal(CtrlAltH, saved.MainHotKey);
            Assert.Equal(UserSettingsSnapshot.Defaults.TaskbarHotKey, saved.TaskbarHotKey);
            Assert.Equal(CtrlAltH, Assert.Single(_applied).Item1);
            Assert.Equal(1, _closed);
        }

        [Theory]
        [InlineData("A")]
        [InlineData("ABA")]
        [InlineData("AB1")]
        public void Save_InvalidAlphabet_ShowsErrorAndSavesNothing(string alphabet)
        {
            var vm = Create();
            vm.HintAlphabetText = alphabet;

            Assert.False(vm.Save());

            Assert.NotNull(vm.HintAlphabetError);
            AssertNothingDone();
        }

        [Theory]
        [InlineData("5")]
        [InlineData("big")]
        [InlineData("")]
        public void Save_InvalidFontSize_ShowsErrorAndSavesNothing(string fontSize)
        {
            var vm = Create();
            vm.FontSizeText = fontSize;

            Assert.False(vm.Save());

            Assert.NotNull(vm.FontSizeError);
            AssertNothingDone();
        }

        [Fact]
        public void Save_SameHotKeyTwice_ShowsErrorAndSavesNothing()
        {
            var vm = Create();
            vm.TaskbarHotKey.CaptureCommand.Execute(UserSettingsSnapshot.Defaults.MainHotKey);

            Assert.False(vm.Save());

            Assert.NotNull(vm.TaskbarHotKey.Error);
            AssertNothingDone();
        }

        [Fact]
        public void Save_HotKeyTaken_ShowsErrorInlineAndSavesNothing()
        {
            var vm = Create();
            _taken.Add(CtrlAltH);
            vm.TaskbarHotKey.CaptureCommand.Execute(CtrlAltH);

            Assert.False(vm.Save());

            Assert.Contains("already used by another app", vm.TaskbarHotKey.Error);
            Assert.Null(vm.MainHotKey.Error);
            Assert.Empty(_settings.Saved);
            Assert.Equal(0, _closed);
        }

        [Fact]
        public void Save_WriteFails_ShowsErrorAndRestoresHotKeys()
        {
            var vm = Create();
            _settings.SaveError = new ConfigurationErrorsException("disk full");
            vm.MainHotKey.CaptureCommand.Execute(CtrlAltH);

            Assert.False(vm.Save());

            Assert.Contains("disk full", vm.SaveError);
            Assert.Equal(2, _applied.Count);
            Assert.Equal(UserSettingsSnapshot.Defaults.MainHotKey, _applied[1].Item1);
            Assert.Equal(0, _closed);
        }

        [Fact]
        public void Capture_InvalidCombination_KeepsCurrentAndShowsError()
        {
            var vm = Create();

            vm.MainHotKey.CaptureCommand.Execute(new HotKeyCombination(KeyModifier.Shift, Keys.Q));

            Assert.Equal("Alt+;", vm.MainHotKey.DisplayText);
            Assert.NotNull(vm.MainHotKey.Error);
        }

        [Fact]
        public void Reset_RestoresDefault()
        {
            var vm = Create();
            vm.MainHotKey.CaptureCommand.Execute(CtrlAltH);

            vm.MainHotKey.ResetCommand.Execute(null);

            Assert.Equal(UserSettingsSnapshot.Defaults.MainHotKey, vm.MainHotKey.Combination);
            Assert.Null(vm.MainHotKey.Error);
        }

        [Fact]
        public void HotKeyCapture_SuspendsAndResumesHotKeys()
        {
            var vm = Create();

            vm.BeginHotKeyCapture();
            vm.EndHotKeyCapture();

            Assert.Equal(new[] { true, false }, _suspended);
        }

        [Fact]
        public void Save_Success_InvokesCloseOnce()
        {
            var vm = Create();

            Assert.True(vm.Save());

            Assert.Equal(1, _closed);
        }

        [Fact]
        public void Save_ApplyFailsWhileCapturing_SuspendsHotKeysAgain()
        {
            var vm = Create();
            vm.BeginHotKeyCapture();
            _taken.Add(CtrlAltH);
            vm.MainHotKey.CaptureCommand.Execute(CtrlAltH);

            Assert.False(vm.Save());

            Assert.Equal(new[] { true, true }, _suspended);
        }

        [Fact]
        public void Save_ApplyFailsNotCapturing_DoesNotSuspend()
        {
            var vm = Create();
            _taken.Add(CtrlAltH);
            vm.MainHotKey.CaptureCommand.Execute(CtrlAltH);

            Assert.False(vm.Save());

            Assert.Empty(_suspended);
        }

        [Fact]
        public void Save_RollbackCouldNotRestoreOldHotKey_SaysSo()
        {
            var vm = Create();
            _taken.Add(CtrlAltH);
            _restoreFailures.Add(UserSettingsSnapshot.Defaults.MainHotKey);
            vm.MainHotKey.CaptureCommand.Execute(CtrlAltH);

            Assert.False(vm.Save());

            Assert.Contains("already used by another app", vm.MainHotKey.Error);
            Assert.Contains("no longer work", vm.SaveError);
            Assert.Contains("Alt+;", vm.SaveError);
            Assert.Empty(_settings.Saved);
        }

        [Fact]
        public void Save_WriteFailsAndOldHotKeyNotRestored_SaysSo()
        {
            var vm = Create();
            _settings.SaveError = new ConfigurationErrorsException("disk full");
            vm.MainHotKey.CaptureCommand.Execute(CtrlAltH);
            _taken.Add(UserSettingsSnapshot.Defaults.MainHotKey); // taken by another app meanwhile

            Assert.False(vm.Save());

            Assert.Contains("disk full", vm.SaveError);
            Assert.Contains("no longer work", vm.SaveError);
        }

        private OptionsViewModel Create()
        {
            return new OptionsViewModel(_settings, Apply, _suspended.Add) { Close = () => _closed++ };
        }

        private IReadOnlyList<HotKeyFailure> Apply(HotKeyCombination main, HotKeyCombination taskbar)
        {
            _applied.Add(Tuple.Create(main, taskbar));
            var failures = new[] { main, taskbar }
                .Where(_taken.Contains)
                .Select(x => new HotKeyFailure(x.ToHotKey(), HotKeyFailure.ErrorHotKeyAlreadyRegistered))
                .ToList();
            if (failures.Count > 0)
            {
                failures.AddRange(_restoreFailures.Select(
                    x => new HotKeyFailure(x.ToHotKey(), HotKeyFailure.ErrorHotKeyAlreadyRegistered, isRestore: true)));
            }
            return failures;
        }

        /// <summary>Previous hotkeys the fake rollback fails to restore</summary>
        private readonly List<HotKeyCombination> _restoreFailures = new List<HotKeyCombination>();

        private void AssertNothingDone()
        {
            Assert.Empty(_settings.Saved);
            Assert.Empty(_applied);
            Assert.Equal(0, _closed);
        }
    }
}
