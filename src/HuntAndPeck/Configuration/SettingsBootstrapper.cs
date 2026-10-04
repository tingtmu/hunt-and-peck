using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using HuntAndPeck.Properties;

namespace HuntAndPeck.Configuration
{
    /// <summary>
    /// Loads the user settings at startup: carries settings over from the previous version and recovers from a
    /// damaged user.config by setting it aside and continuing with defaults
    /// </summary>
    internal static class SettingsBootstrapper
    {
        /// <summary>
        /// Both the current and the previous version's user.config can be damaged; allow a few files
        /// </summary>
        private const int MaxRecoveries = 3;

        /// <summary>
        /// Loads (and if needed upgrades) the settings
        /// </summary>
        /// <returns>A message for the user if a damaged settings file was reset, else null</returns>
        /// <exception cref="ConfigurationErrorsException">The settings could not be recovered</exception>
        public static string Initialize()
        {
            string warning = null;
            for (var attempt = 0; ; ++attempt)
            {
                try
                {
                    CheckUserConfigFiles();
                    UpgradeIfRequired(Settings.Default);
                    return warning;
                }
                catch (ConfigurationErrorsException ex) when (attempt < MaxRecoveries)
                {
                    Trace.TraceError("Settings: loading the user settings failed: {0}", ex);
                    warning = SetAside(CorruptSettingsFile.FindPath(ex));
                    if (warning == null)
                    {
                        throw;
                    }
                    Settings.Default.Reload();
                }
            }
        }

        /// <summary>
        /// Parses the user.config files on their own, before the settings are first read: once the static
        /// configuration system has failed on a damaged file it caches the error for the rest of the process,
        /// so the file must be set aside before that happens
        /// </summary>
        /// <exception cref="ConfigurationErrorsException">A user.config file is damaged</exception>
        private static void CheckUserConfigFiles()
        {
            ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
        }

        private static void UpgradeIfRequired(Settings settings)
        {
            // Reading a value loads the user.config, which throws if it is damaged
            if (!settings.UpgradeRequired)
            {
                return;
            }

            Trace.TraceInformation("Settings: upgrading from the previous version, if any");
            settings.Upgrade();
            settings.UpgradeRequired = false;
            settings.Save();
        }

        /// <summary>
        /// Renames the damaged file so the defaults load next. Only a user.config under %LOCALAPPDATA% or
        /// %APPDATA% is touched (never hap.exe.config or machine.config), and it is never deleted.
        /// </summary>
        /// <returns>A message for the user, else null if the file may not or could not be renamed</returns>
        private static string SetAside(string path)
        {
            var allowedRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            };
            if (!CorruptSettingsFile.IsUserSettingsFile(path, allowedRoots))
            {
                Trace.TraceError("Settings: the damaged file ({0}) is not a user settings file, leaving it alone", path ?? "unknown");
                return null;
            }
            if (!File.Exists(path))
            {
                Trace.TraceError("Settings: the damaged settings file {0} is missing, cannot recover", path);
                return null;
            }

            var backupPath = CorruptSettingsFile.BackupPath(path, DateTime.Now);
            try
            {
                File.Move(path, backupPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.TraceError("Settings: renaming damaged {0} to {1} failed: {2}", path, backupPath, ex);
                return null;
            }

            Trace.TraceWarning("Settings: renamed damaged {0} to {1}; using defaults", path, backupPath);
            return string.Format("Your settings file was damaged, so HuntAndPeck reset its settings to the defaults. The damaged file was kept as {0}", backupPath);
        }
    }
}
