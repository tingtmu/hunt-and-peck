namespace HuntAndPeck.Services.Uia
{
    /// <summary>
    /// Where an element's properties are read from
    /// </summary>
    internal enum UiaPropertySource
    {
        /// <summary>The element's cache, filled in bulk by the enumeration's cache request (no cross-process call)</summary>
        Cached,

        /// <summary>Live from the target, one cross-process call per property</summary>
        Current,
    }
}
