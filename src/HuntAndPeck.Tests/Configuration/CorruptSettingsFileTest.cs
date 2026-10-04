using System;
using System.Configuration;
using HuntAndPeck.Configuration;
using Xunit;

namespace HuntAndPeck.Tests.Configuration
{
    public class CorruptSettingsFileTest
    {
        private const string UserConfig = @"C:\Users\me\AppData\Local\HuntAndPeck\hap.exe_Url_x\1.7.0.0\user.config";

        [Fact]
        public void FindPath_DirectFilename()
        {
            var ex = new ConfigurationErrorsException("bad", UserConfig, 3);

            Assert.Equal(UserConfig, CorruptSettingsFile.FindPath(ex));
        }

        [Fact]
        public void FindPath_InnerFilename()
        {
            // The settings provider throws "Configuration system failed to initialize" around the file error
            var ex = new ConfigurationErrorsException("failed to initialize", new ConfigurationErrorsException("bad", UserConfig, 1));

            Assert.Equal(UserConfig, CorruptSettingsFile.FindPath(ex));
        }

        [Fact]
        public void FindPath_NoFilename_ReturnsNull()
        {
            var ex = new ConfigurationErrorsException("bad", new InvalidOperationException("inner"));

            Assert.Null(CorruptSettingsFile.FindPath(ex));
            Assert.Null(CorruptSettingsFile.FindPath(null));
        }

        [Fact]
        public void BackupPath_AppendsTimestamp()
        {
            var path = CorruptSettingsFile.BackupPath(UserConfig, new DateTime(2026, 10, 4, 9, 5, 7));

            Assert.Equal(UserConfig + ".corrupt-20261004-090507", path);
        }

        private const string Local = @"C:\Users\me\AppData\Local";
        private const string Roaming = @"C:\Users\me\AppData\Roaming";

        [Theory]
        [InlineData(UserConfig)]
        [InlineData(@"C:\Users\me\AppData\Roaming\HuntAndPeck\hap.exe_Url_x\1.7.0.0\user.config")]
        [InlineData(@"c:\users\ME\appdata\local\HuntAndPeck\x\USER.CONFIG")]
        public void IsUserSettingsFile_UserConfigUnderProfile_IsAllowed(string path)
        {
            Assert.True(CorruptSettingsFile.IsUserSettingsFile(path, Local, Roaming));
        }

        [Theory]
        [InlineData(@"C:\Program Files\HuntAndPeck\hap.exe.config")]
        [InlineData(@"C:\Users\me\AppData\Local\HuntAndPeck\hap.exe.config")]
        [InlineData(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config")]
        [InlineData(@"C:\Users\me\AppData\LocalEvil\user.config")]
        [InlineData(@"C:\Users\me\AppData\Local\..\LocalEvil\user.config")]
        [InlineData(@"C:\Program Files\HuntAndPeck\user.config")]
        [InlineData(@"C:\Users\me\AppData\Local\HuntAndPeck\user.config.bak")]
        [InlineData(@"C:\Users\me\AppData\Local")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("C:\\bad|path\\user.config")]
        public void IsUserSettingsFile_AnythingElse_IsRefused(string path)
        {
            Assert.False(CorruptSettingsFile.IsUserSettingsFile(path, Local, Roaming));
        }

        [Fact]
        public void IsUserSettingsFile_RootWithTrailingSeparator_Works()
        {
            Assert.True(CorruptSettingsFile.IsUserSettingsFile(UserConfig, Local + "\\"));
            Assert.False(CorruptSettingsFile.IsUserSettingsFile(UserConfig, null, ""));
        }
    }
}
