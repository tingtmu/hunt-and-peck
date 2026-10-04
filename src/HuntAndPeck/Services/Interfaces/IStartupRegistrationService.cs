namespace HuntAndPeck.Services.Interfaces
{
    /// <summary>
    /// Registers HuntAndPeck to start (in tray mode) when the current user signs in to Windows
    /// </summary>
    /// <remarks>
    /// Every member may throw <see cref="System.Security.SecurityException"/>,
    /// <see cref="System.UnauthorizedAccessException"/> or <see cref="System.IO.IOException"/>.
    /// </remarks>
    internal interface IStartupRegistrationService
    {
        /// <summary>
        /// True if Windows will start HuntAndPeck at sign-in: registered, pointing to an existing file,
        /// and not disabled (e.g. in Task Manager's Startup apps)
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Registers the running exe, re-enabling it if it was disabled in Task Manager
        /// </summary>
        void Enable();

        /// <summary>
        /// Removes the registration
        /// </summary>
        void Disable();

        /// <summary>
        /// If registered for a different exe path (the exe was moved), points the registration to the running
        /// exe. Never creates a registration.
        /// </summary>
        /// <returns>True if the registration was updated</returns>
        bool UpdatePathIfMoved();
    }
}
