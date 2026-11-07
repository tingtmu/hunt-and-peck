using System.Threading;
using System.Threading.Tasks;

namespace HuntAndPeck.Updates
{
    /// <summary>
    /// Fetches the latest release; the seam that keeps the network out of the update logic
    /// </summary>
    internal interface IReleaseClient
    {
        /// <summary>
        /// Gets the latest release
        /// </summary>
        /// <exception cref="System.Exception">The release could not be fetched or read (network, HTTP status, bad JSON)</exception>
        Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellation);
    }
}
