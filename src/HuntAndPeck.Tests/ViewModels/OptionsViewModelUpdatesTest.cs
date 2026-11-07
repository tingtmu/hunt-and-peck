using System.Collections.Generic;
using HuntAndPeck.Configuration;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services.Interfaces;
using HuntAndPeck.ViewModels;
using Xunit;

namespace HuntAndPeck.Tests.ViewModels
{
    /// <summary>The "Check for updates automatically" option of <see cref="OptionsViewModel"/></summary>
    public class OptionsViewModelUpdatesTest
    {
        private readonly FakeUserSettings _settings = new FakeUserSettings();
        private int _closed;

        [Fact]
        public void Defaults_CheckForUpdatesIsOn()
        {
            Assert.True(UserSettingsSnapshot.Defaults.CheckForUpdates);
            Assert.True(Create().CheckForUpdates);
        }

        [Fact]
        public void Constructor_ShowsTheStoredValue()
        {
            var settings = new FakeUserSettings(UserSettingsSnapshot.Defaults.WithCheckForUpdates(false));

            Assert.False(Create(settings).CheckForUpdates);
        }

        [Fact]
        public void Save_PersistsTheChoice()
        {
            var vm = Create();
            vm.CheckForUpdates = false;

            Assert.True(vm.Save());

            Assert.False(Assert.Single(_settings.Saved).CheckForUpdates);
            Assert.Equal(1, _closed);
        }

        [Fact]
        public void Save_Unchanged_KeepsItOn()
        {
            Assert.True(Create().Save());

            Assert.True(Assert.Single(_settings.Saved).CheckForUpdates);
        }

        [Fact]
        public void Edit_WithoutSave_PersistsNothing()
        {
            Create().CheckForUpdates = false;

            Assert.Empty(_settings.Saved);
            Assert.True(_settings.Current.CheckForUpdates);
        }

        [Fact]
        public void Save_InvalidOtherField_PersistsNothing()
        {
            var vm = Create();
            vm.CheckForUpdates = false;
            vm.FontSizeText = "huge";

            Assert.False(vm.Save());

            Assert.Empty(_settings.Saved);
        }

        [Fact]
        public void Changing_RaisesPropertyChanged()
        {
            var vm = Create();
            var changed = new List<string>();
            vm.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            vm.CheckForUpdates = false;

            Assert.Equal(new[] { nameof(OptionsViewModel.CheckForUpdates) }, changed);
        }

        [Fact]
        public void WithCheckForUpdates_KeepsTheOtherSettings()
        {
            var original = new UserSettingsSnapshot(
                20, "JKL", new HotKeyCombination(KeyModifier.Control, System.Windows.Forms.Keys.H), UserSettingsSnapshot.Defaults.TaskbarHotKey, HintStyle.Theme);

            var copy = original.WithCheckForUpdates(false);

            Assert.False(copy.CheckForUpdates);
            Assert.Equal(original.FontSize, copy.FontSize);
            Assert.Equal(original.HintAlphabet, copy.HintAlphabet);
            Assert.Equal(original.MainHotKey, copy.MainHotKey);
            Assert.Equal(original.TaskbarHotKey, copy.TaskbarHotKey);
            Assert.Equal(original.HintStyle, copy.HintStyle);
        }

        private OptionsViewModel Create(FakeUserSettings settings = null)
        {
            return new OptionsViewModel(settings ?? _settings, (main, taskbar) => new HotKeyFailure[0], suspended => { }) { Close = () => _closed++ };
        }
    }
}
