namespace Game.Platform.Abstractions
{
    /// <summary>
    /// Provides the platform achievement boundary.
    /// </summary>
    public interface IAchievementService
    {
    }

    /// <summary>
    /// Provides the platform statistics boundary.
    /// </summary>
    public interface IPlatformStatsService
    {
    }

    /// <summary>
    /// Provides the platform cloud synchronization boundary.
    /// </summary>
    public interface ICloudSyncService
    {
    }

    /// <summary>
    /// Provides the platform rich-presence boundary.
    /// </summary>
    public interface IRichPresenceService
    {
    }

    /// <summary>
    /// Provides the platform user identity boundary.
    /// </summary>
    public interface IUserIdentityService
    {
    }

    /// <summary>
    /// Isolates application code from a concrete platform SDK.
    /// </summary>
    public interface IPlatformFacade
    {
        /// <summary>
        /// Gets a value indicating whether a real platform backend is available.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets the achievement service.
        /// </summary>
        IAchievementService Achievements { get; }

        /// <summary>
        /// Gets the statistics service.
        /// </summary>
        IPlatformStatsService Stats { get; }

        /// <summary>
        /// Gets the cloud synchronization service.
        /// </summary>
        ICloudSyncService Cloud { get; }

        /// <summary>
        /// Gets the rich-presence service.
        /// </summary>
        IRichPresenceService RichPresence { get; }

        /// <summary>
        /// Gets the user identity service.
        /// </summary>
        IUserIdentityService Identity { get; }
    }
}
