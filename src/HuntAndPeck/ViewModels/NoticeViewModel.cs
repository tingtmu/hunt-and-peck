using System.Windows;

namespace HuntAndPeck.ViewModels
{
    /// <summary>
    /// A short message shown where the overlay would have been, e.g. when a window has nothing to click
    /// </summary>
    public class NoticeViewModel : IOverlayBounds
    {
        public NoticeViewModel(string message, Rect bounds)
        {
            Message = message;
            Bounds = bounds;
        }

        public string Message { get; }

        /// <summary>The window the notice is about, physical screen pixels</summary>
        public Rect Bounds { get; }
    }
}
