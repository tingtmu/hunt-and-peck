using System.Collections.Generic;
using System.Configuration;
using HuntAndPeck.Configuration;

namespace HuntAndPeck.Tests.ViewModels
{
    /// <summary>
    /// In-memory settings; records saves and can fail them
    /// </summary>
    internal sealed class FakeUserSettings : IUserSettings
    {
        public FakeUserSettings(UserSettingsSnapshot current = null)
        {
            Current = current ?? UserSettingsSnapshot.Defaults;
        }

        public UserSettingsSnapshot Current { get; private set; }

        public List<UserSettingsSnapshot> Saved { get; } = new List<UserSettingsSnapshot>();

        public ConfigurationErrorsException SaveError { get; set; }

        public UserSettingsSnapshot Load() => Current;

        public void Save(UserSettingsSnapshot settings)
        {
            if (SaveError != null)
            {
                throw SaveError;
            }
            Saved.Add(settings);
            Current = settings;
        }
    }
}
