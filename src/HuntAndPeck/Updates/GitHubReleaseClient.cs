using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HuntAndPeck.Updates
{
    /// <summary>
    /// Reads the latest release from the GitHub REST API (https only, no authentication)
    /// </summary>
    internal sealed class GitHubReleaseClient : IReleaseClient
    {
        public const string LatestReleaseUrl = "https://api.github.com/repos/tingtmu/hunt-and-peck/releases/latest";
        public const string UserAgent = "HuntAndPeck-update-check";
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient _http;

        public GitHubReleaseClient()
        {
            // Never below TLS 1.2, whatever the machine's default is
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            _http = new HttpClient { Timeout = Timeout };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        public async Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellation)
        {
            // ConfigureAwait(false): nothing here needs the UI thread
            using (var response = await _http.GetAsync(LatestReleaseUrl, cancellation).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseRelease(json);
            }
        }

        /// <summary>
        /// Reads tag_name and html_url from a GitHub release JSON document
        /// </summary>
        /// <exception cref="FormatException">The text is not JSON, or lacks a tag_name or an https html_url</exception>
        public static ReleaseInfo ParseRelease(string json)
        {
            var document = Deserialize(json);
            if (string.IsNullOrWhiteSpace(document?.TagName))
            {
                throw new FormatException("The release has no tag_name.");
            }

            Uri url;
            if (!Uri.TryCreate(document.HtmlUrl, UriKind.Absolute, out url) || url.Scheme != Uri.UriSchemeHttps)
            {
                throw new FormatException("The release has no https html_url.");
            }
            return new ReleaseInfo(document.TagName.Trim(), url.AbsoluteUri);
        }

        private static ReleaseDocument Deserialize(string json)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ReleaseDocument));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
                {
                    return (ReleaseDocument)serializer.ReadObject(stream);
                }
            }
            catch (SerializationException ex)
            {
                throw new FormatException("The release is not valid JSON: " + ex.Message, ex);
            }
        }

        [DataContract]
        private sealed class ReleaseDocument
        {
            [DataMember(Name = "tag_name")]
            public string TagName { get; set; }

            [DataMember(Name = "html_url")]
            public string HtmlUrl { get; set; }
        }
    }
}
