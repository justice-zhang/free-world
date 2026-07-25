using Game.Platform.Abstractions;

namespace Game.Platform.Null
{
    /// <summary>
    /// Provides deterministic no-op platform services when no platform SDK is present.
    /// </summary>
    public sealed class NullPlatformFacade : IPlatformFacade
    {
        private readonly IAchievementService achievements = new NullAchievementService();
        private readonly IPlatformStatsService stats = new NullPlatformStatsService();
        private readonly ICloudSyncService cloud = new NullCloudSyncService();
        private readonly IRichPresenceService richPresence = new NullRichPresenceService();
        private readonly IUserIdentityService identity = new NullUserIdentityService();

        /// <inheritdoc />
        public bool IsAvailable => false;

        /// <inheritdoc />
        public IAchievementService Achievements => achievements;

        /// <inheritdoc />
        public IPlatformStatsService Stats => stats;

        /// <inheritdoc />
        public ICloudSyncService Cloud => cloud;

        /// <inheritdoc />
        public IRichPresenceService RichPresence => richPresence;

        /// <inheritdoc />
        public IUserIdentityService Identity => identity;

        private sealed class NullAchievementService : IAchievementService
        {
        }

        private sealed class NullPlatformStatsService : IPlatformStatsService
        {
        }

        private sealed class NullCloudSyncService : ICloudSyncService
        {
        }

        private sealed class NullRichPresenceService : IRichPresenceService
        {
        }

        private sealed class NullUserIdentityService : IUserIdentityService
        {
        }
    }
}
