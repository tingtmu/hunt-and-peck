using HuntAndPeck.Updates;
using Xunit;

namespace HuntAndPeck.Tests.Updates
{
    public class ScoopInstallTest
    {
        [Theory]
        [InlineData(@"C:\Users\me\scoop\apps\huntandpeck\current\hap.exe")]
        [InlineData(@"C:\Users\me\scoop\apps\huntandpeck\1.10.1\hap.exe")]
        [InlineData(@"D:\Tools\Scoop\Apps\HuntAndPeck\current\hap.exe")]
        [InlineData(@"C:\ProgramData\scoop\apps\huntandpeck\current\hap.exe")]
        [InlineData("C:/Users/me/scoop/apps/huntandpeck/current/hap.exe")]
        public void IsScoopPath_UnderScoopApps_IsTrue(string path)
        {
            Assert.True(ScoopInstall.IsScoopPath(path));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(@"C:\Program Files\Hunt And Peck\hap.exe")]
        [InlineData(@"C:\Users\me\scoop\shims\hap.exe")]
        [InlineData(@"C:\Users\me\scoop\persist\huntandpeck\hap.exe")]
        [InlineData(@"C:\Users\me\myscoop\apps\huntandpeck\hap.exe")]
        [InlineData(@"C:\scoop\appsx\huntandpeck\hap.exe")]
        public void IsScoopPath_Elsewhere_IsFalse(string path)
        {
            Assert.False(ScoopInstall.IsScoopPath(path));
        }
    }
}
