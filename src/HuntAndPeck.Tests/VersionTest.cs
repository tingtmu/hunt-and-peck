using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace HuntAndPeck.Tests
{
    public class VersionTest
    {
        /// <summary>app.manifest can't take its version from &lt;Version&gt; in src\Directory.Build.props, so the two are kept in sync by hand</summary>
        [Fact]
        public void ManifestVersion_MatchesAssemblyVersion()
        {
            // Copied next to the test assembly by the test project
            var manifest = XDocument.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.manifest"));
            XNamespace asmV1 = "urn:schemas-microsoft-com:asm.v1";
            var manifestVersion = manifest.Root.Element(asmV1 + "assemblyIdentity").Attribute("version").Value;

            Assert.Equal(typeof(App).Assembly.GetName().Version.ToString(), manifestVersion);
        }
    }
}
