using System;
using System.Windows;

namespace HuntAndPeck.Models
{
    /// <summary>
    /// Represents a hint that has 1 or more capabilities
    /// </summary>
    public abstract class Hint
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="owningWindow">The owning window</param>
        /// <param name="boundingRectangle">The bounding rectangle of the hint in owner window coordinates</param>
        protected Hint(IntPtr owningWindow, Rect boundingRectangle)
        {
            OwningWindow = owningWindow;
            BoundingRectangle = boundingRectangle;
        }

        /// <summary>
        /// The bounding rectangle for the hint in Window coordinates for the owning window
        /// </summary>
        public Rect BoundingRectangle { get; private set; }

        /// <summary>
        /// The window handle of the owning window
        /// </summary>
        public IntPtr OwningWindow { get; private set; }

        /// <summary>
        /// True if the overlay must close before the hint is invoked, e.g. because the hint clicks the screen
        /// where the overlay is
        /// </summary>
        public virtual bool InvokeAfterOverlayCloses => false;

        /// <summary>
        /// A hint that clicks the same element with the mouse, for a forced click (Shift) and, if
        /// <see cref="ClicksOnFailure"/>, when this hint's action fails; null if there is none (e.g. this hint
        /// already clicks)
        /// </summary>
        public virtual Hint CreateClickHint() => null;

        /// <summary>
        /// True if a failed action may be retried as a click (<see cref="CreateClickHint"/>). False where a
        /// click could do something else than the action, e.g. change a slider's value instead of focusing it.
        /// </summary>
        public virtual bool ClicksOnFailure => false;

        /// <summary>
        /// Invokes the hint
        /// </summary>
        public abstract void Invoke();
    }
}
