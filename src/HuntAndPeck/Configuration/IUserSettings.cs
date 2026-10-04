using System.Configuration;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Persisted user settings
    /// </summary>
    internal interface IUserSettings
    {
        /// <summary>
        /// Reads the settings; invalid stored values are logged and replaced by their defaults
        /// </summary>
        UserSettingsSnapshot Load();

        /// <summary>
        /// Stores the settings
        /// </summary>
        /// <exception cref="ConfigurationErrorsException">Writing the settings file failed</exception>
        void Save(UserSettingsSnapshot settings);
    }
}
