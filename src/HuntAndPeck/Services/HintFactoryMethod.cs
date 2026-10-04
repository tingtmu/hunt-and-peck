using System;
using System.Windows;
using HuntAndPeck.Models;
using HuntAndPeck.Services.Uia;
using UIAutomationClient;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Creates the hint for one enumerated element. Runs on the UIA worker thread.
    /// </summary>
    /// <param name="owningWindow">The window being enumerated</param>
    /// <param name="hintBounds">The element's bounds in owning window coordinates</param>
    /// <param name="automationElement">The element</param>
    /// <param name="source">Whether the element's properties are cached or must be read live</param>
    /// <returns>The hint, else null if the element gets none</returns>
    internal delegate Hint HintFactoryMethod(
        IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement, UiaPropertySource source);
}
