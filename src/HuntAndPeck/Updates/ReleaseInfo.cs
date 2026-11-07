namespace HuntAndPeck.Updates
{
    /// <summary>
    /// The latest published release, as the update check reads it
    /// </summary>
    internal sealed class ReleaseInfo
    {
        public ReleaseInfo(string tag, string pageUrl)
        {
            Tag = tag;
            PageUrl = pageUrl;
        }

        /// <summary>The release's tag name, e.g. "v1.10.1"</summary>
        public string Tag { get; }

        /// <summary>The release's web page (https)</summary>
        public string PageUrl { get; }
    }
}
