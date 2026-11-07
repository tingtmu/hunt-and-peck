using System;
using HuntAndPeck.Updates;
using Xunit;

namespace HuntAndPeck.Tests.Updates
{
    public class ReleaseVersionTest
    {
        [Theory]
        [InlineData("v1.10.1", "1.10.1.0")]
        [InlineData("1.10.1", "1.10.1.0")]
        [InlineData("V2.0.0", "2.0.0.0")]
        [InlineData(" v1.11 ", "1.11.0.0")]
        [InlineData("1.2.3.4", "1.2.3.4")]
        public void TryParse_Valid(string tag, string expected)
        {
            Version version;

            Assert.True(ReleaseVersion.TryParse(tag, out version));
            Assert.Equal(new Version(expected), version);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("v")]
        [InlineData("latest")]
        [InlineData("1")]
        [InlineData("v1.10.1-beta")]
        [InlineData("v1.10.1+build5")]
        [InlineData("v1..1")]
        [InlineData("v1.2.3.4.5")]
        [InlineData("vv1.2.3")]
        [InlineData("1.2.x")]
        [InlineData("-1.2.3")]
        [InlineData("v1.2.99999999")]
        public void TryParse_Invalid(string tag)
        {
            Version version;

            Assert.False(ReleaseVersion.TryParse(tag, out version));
            Assert.Null(version);
        }

        [Theory]
        [InlineData("v1.10.2", "1.10.1.0", true)]
        [InlineData("1.11.0", "1.10.1.0", true)]
        [InlineData("v2.0", "1.10.1.0", true)]
        [InlineData("v1.10.1.1", "1.10.1.0", true)]
        [InlineData("v1.10.1", "1.10.1.0", false)]
        [InlineData("1.10.1", "1.10.1", false)]
        [InlineData("v1.9.9", "1.10.1.0", false)]
        [InlineData("v1.10", "1.10.0.0", false)]
        [InlineData("v1.9.0", "1.10.1.0", false)]
        public void IsNewer_ComparesWithTheRunningVersion(string tag, string current, bool expected)
        {
            Version latest;

            Assert.Equal(expected, ReleaseVersion.IsNewer(tag, new Version(current), out latest));
            Assert.NotNull(latest);
        }

        [Theory]
        [InlineData("not a version")]
        [InlineData("v1.12.0-rc1")]
        [InlineData(null)]
        public void IsNewer_InvalidTag_IsNotNewer(string tag)
        {
            Version latest;

            Assert.False(ReleaseVersion.IsNewer(tag, new Version(1, 10, 1, 0), out latest));
            Assert.Null(latest);
        }

        [Theory]
        [InlineData("1.10.1.0", "1.10.1")]
        [InlineData("1.11", "1.11.0")]
        [InlineData("1.2.3.4", "1.2.3.4")]
        public void Format_ShowsThreePartsUnlessThereIsARevision(string version, string expected)
        {
            Assert.Equal(expected, ReleaseVersion.Format(new Version(version)));
        }
    }
}
