using System;
using System.Collections.Generic;
using HuntAndPeck.Services;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class StartupRegistrationServiceTest
    {
        private const string Name = StartupRegistrationService.ValueName;
        private const string ExePath = @"C:\Program Files\Hunt And Peck\hap.exe";
        private const string OldExePath = @"C:\Old Place\hap.exe";

        private static readonly byte[] Disabled = { 0x03, 0, 0, 0, 0x5D, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E, 0xD9, 0x01 };

        private readonly FakeRegistryRunKey _registry = new FakeRegistryRunKey();
        private readonly HashSet<string> _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ExePath, OldExePath };

        private StartupRegistrationService CreateService()
        {
            return new StartupRegistrationService(_registry, ExePath, _files.Contains);
        }

        [Fact]
        public void Default_IsDisabled()
        {
            Assert.False(CreateService().IsEnabled);
        }

        [Fact]
        public void Enable_WritesQuotedExePath_AndIsEnabled()
        {
            var service = CreateService();

            service.Enable();

            Assert.Equal("\"" + ExePath + "\"", _registry.Run[Name]);
            Assert.True(service.IsEnabled);
            Assert.Empty(_registry.StartupApproved);
        }

        [Fact]
        public void Disable_RemovesRunAndStartupApprovedValues()
        {
            var service = CreateService();
            service.Enable();
            _registry.StartupApproved[Name] = Disabled;

            service.Disable();

            Assert.False(service.IsEnabled);
            Assert.Empty(_registry.Run);
            Assert.Empty(_registry.StartupApproved);
        }

        [Fact]
        public void Disable_WhenAbsent_DoesNotThrow()
        {
            var service = CreateService();

            service.Disable();

            Assert.False(service.IsEnabled);
        }

        [Fact]
        public void IsEnabled_DisabledInStartupApproved_IsFalse()
        {
            _registry.Run[Name] = "\"" + ExePath + "\"";
            _registry.StartupApproved[Name] = Disabled;

            Assert.False(CreateService().IsEnabled);
        }

        [Fact]
        public void Enable_WhenDisabledInStartupApproved_ReApproves()
        {
            _registry.Run[Name] = "\"" + ExePath + "\"";
            _registry.StartupApproved[Name] = Disabled;
            var service = CreateService();

            service.Enable();

            Assert.True(service.IsEnabled);
            Assert.Equal(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, _registry.StartupApproved[Name]);
        }

        [Fact]
        public void Enable_WhenApprovedEnabled_LeavesStartupApprovedAlone()
        {
            var approved = new byte[] { 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            _registry.StartupApproved[Name] = approved;

            CreateService().Enable();

            Assert.Same(approved, _registry.StartupApproved[Name]);
        }

        [Fact]
        public void IsEnabled_RegisteredFileMissing_IsFalse()
        {
            _registry.Run[Name] = "\"" + @"C:\Gone\hap.exe" + "\"";

            Assert.False(CreateService().IsEnabled);
        }

        [Fact]
        public void IsEnabled_UnquotedPath_IsTrue()
        {
            _registry.Run[Name] = ExePath;

            Assert.True(CreateService().IsEnabled);
        }

        [Theory]
        [InlineData(new byte[] { 0x02 }, true)]
        [InlineData(new byte[] { 0x06, 0, 0, 0 }, true)]
        [InlineData(new byte[] { 0x03, 0, 0, 0 }, false)]
        [InlineData(new byte[] { 0x07, 0, 0, 0 }, false)]
        [InlineData(new byte[] { 0x01 }, false)]
        [InlineData(new byte[] { 0x09 }, false)]
        [InlineData(new byte[0], true)]
        [InlineData(null, true)]
        public void IsApproved_OddFirstByteMeansDisabled(byte[] value, bool expected)
        {
            Assert.Equal(expected, StartupRegistrationService.IsApproved(value));
        }

        [Fact]
        public void UpdatePathIfMoved_DifferentPath_RewritesToCurrentExe()
        {
            _registry.Run[Name] = "\"" + OldExePath + "\"";
            var service = CreateService();

            Assert.True(service.UpdatePathIfMoved());

            Assert.Equal("\"" + ExePath + "\"", _registry.Run[Name]);
        }

        [Fact]
        public void UpdatePathIfMoved_Absent_StaysAbsent()
        {
            Assert.False(CreateService().UpdatePathIfMoved());

            Assert.Empty(_registry.Run);
            Assert.Equal(0, _registry.RunWrites);
        }

        [Theory]
        [InlineData("\"" + ExePath + "\"")]
        [InlineData(ExePath)]
        [InlineData("\"C:\\PROGRAM FILES\\Hunt And Peck\\HAP.EXE\"")]
        public void UpdatePathIfMoved_SamePath_DoesNotWrite(string data)
        {
            _registry.Run[Name] = data;

            Assert.False(CreateService().UpdatePathIfMoved());

            Assert.Equal(data, _registry.Run[Name]);
            Assert.Equal(0, _registry.RunWrites);
        }

        [Fact]
        public void UpdatePathIfMoved_DoesNotTouchStartupApproved()
        {
            _registry.Run[Name] = "\"" + OldExePath + "\"";
            _registry.StartupApproved[Name] = Disabled;
            var service = CreateService();

            service.UpdatePathIfMoved();

            Assert.Same(Disabled, _registry.StartupApproved[Name]);
            Assert.False(service.IsEnabled);
        }

        [Theory]
        [InlineData("\"C:\\A B\\hap.exe\"", @"C:\A B\hap.exe")]
        [InlineData("\"C:\\A B\\hap.exe\" /tray", @"C:\A B\hap.exe")]
        [InlineData("  C:\\AB\\hap.exe  ", @"C:\AB\hap.exe")]
        [InlineData("\"C:\\A B\\hap.exe", @"C:\A B\hap.exe")]
        [InlineData("\"\"", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ParseExePath_HandlesQuoting(string data, string expected)
        {
            Assert.Equal(expected, StartupRegistrationService.ParseExePath(data));
        }

        [Fact]
        public void Quote_WrapsPathWithSpacesInQuotes()
        {
            Assert.Equal("\"C:\\A B\\hap.exe\"", StartupRegistrationService.Quote(@"C:\A B\hap.exe"));
        }
    }
}
