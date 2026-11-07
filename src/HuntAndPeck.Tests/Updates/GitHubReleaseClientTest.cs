using System;
using HuntAndPeck.Updates;
using Xunit;

namespace HuntAndPeck.Tests.Updates
{
    public class GitHubReleaseClientTest
    {
        [Fact]
        public void ParseRelease_ReadsTagAndPageUrl_IgnoringOtherFields()
        {
            const string json = @"{
                ""url"": ""https://api.github.com/repos/tingtmu/hunt-and-peck/releases/1"",
                ""html_url"": ""https://github.com/tingtmu/hunt-and-peck/releases/tag/v1.11.0"",
                ""tag_name"": ""v1.11.0"",
                ""draft"": false,
                ""assets"": [ { ""name"": ""HuntAndPeck-1.11.0.zip"", ""size"": 12 } ],
                ""author"": { ""login"": ""x"" }
            }";

            var release = GitHubReleaseClient.ParseRelease(json);

            Assert.Equal("v1.11.0", release.Tag);
            Assert.Equal("https://github.com/tingtmu/hunt-and-peck/releases/tag/v1.11.0", release.PageUrl);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("{}")]
        [InlineData(@"{ ""tag_name"": ""v1.0.0"" }")]
        [InlineData(@"{ ""html_url"": ""https://github.com/x/y/releases/tag/v1.0.0"" }")]
        [InlineData(@"{ ""tag_name"": ""  "", ""html_url"": ""https://github.com/x/y"" }")]
        [InlineData(@"{ ""tag_name"": ""v1.0.0"", ""html_url"": ""http://github.com/x/y"" }")]
        [InlineData(@"{ ""tag_name"": ""v1.0.0"", ""html_url"": ""file:///C:/Windows/notepad.exe"" }")]
        [InlineData(@"{ ""tag_name"": ""v1.0.0"", ""html_url"": ""not a url"" }")]
        public void ParseRelease_Invalid_Throws(string json)
        {
            Assert.Throws<FormatException>(() => GitHubReleaseClient.ParseRelease(json));
        }

        [Fact]
        public void Request_UsesHttpsAndTheDocumentedUserAgentAndTimeout()
        {
            Assert.StartsWith("https://api.github.com/repos/tingtmu/hunt-and-peck/releases/latest", GitHubReleaseClient.LatestReleaseUrl);
            Assert.Equal("HuntAndPeck-update-check", GitHubReleaseClient.UserAgent);
            Assert.Equal(TimeSpan.FromSeconds(10), GitHubReleaseClient.Timeout);
        }
    }
}
