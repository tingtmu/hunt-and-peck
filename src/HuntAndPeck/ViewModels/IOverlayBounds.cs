using System.Windows;

namespace HuntAndPeck.ViewModels
{
    /// <summary>
    /// View model of an overlay that covers a target window
    /// </summary>
    public interface IOverlayBounds
    {
        /// <summary>
        /// Bounds of the target window in physical screen pixels
        /// </summary>
        Rect Bounds { get; }
    }
}
