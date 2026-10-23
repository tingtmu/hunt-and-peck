using System.Reflection;
using Xunit;

namespace HuntAndPeck.Tests
{
    /// <summary>What users see of hap.exe outside the app, set in HuntAndPeck.csproj and src\Directory.Build.props</summary>
    public class AssemblyMetadataTest
    {
        private static readonly Assembly App = typeof(App).Assembly;

        [Fact]
        public void InformationalVersion_IsTheReleaseVersion_WithoutCommitSuffix()
        {
            // The About box shows it; the SDK appends "+<commit sha>" unless IncludeSourceRevisionInInformationalVersion is false
            var informational = App.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            Assert.Equal(App.GetName().Version.ToString(3), informational);
        }

        [Fact]
        public void Title_IsHuntAndPeck()
        {
            // Explorer and Task Manager show it as the file description; the SDK would default it to "hap"
            Assert.Equal("HuntAndPeck", App.GetCustomAttribute<AssemblyTitleAttribute>().Title);
        }

        [Fact]
        public void Exe_IsAnyCpu_AndRuns64BitOn64BitWindows()
        {
            App.ManifestModule.GetPEKind(out var peKind, out _);

            Assert.Equal(PortableExecutableKinds.ILOnly, peKind);
        }
    }
}
