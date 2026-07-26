using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Game.Platform.Abstractions;

namespace Game.Platform.Null
{
    /// <summary>Deterministic no-op platform services used without a platform SDK.</summary>
    public sealed class NullPlatformFacade : IPlatformFacade
    {
        private readonly IAchievementService achievements = new NullAchievementService();
        private readonly IPlatformStatsService stats = new NullPlatformStatsService();
        private readonly ICloudSyncService cloud = new NullCloudSyncService();
        private readonly IRichPresenceService richPresence = new NullRichPresenceService();
        private readonly IUserIdentityService identity = new NullUserIdentityService();

        public bool IsAvailable => false;
        public IAchievementService Achievements => achievements;
        public IPlatformStatsService Stats => stats;
        public ICloudSyncService Cloud => cloud;
        public IRichPresenceService RichPresence => richPresence;
        public IUserIdentityService Identity => identity;

        private sealed class NullAchievementService : IAchievementService
        {
            public ValueTask<PlatformOperationResult> UnlockAsync(ContentId achievementId, CancellationToken cancellationToken = default) =>
                new ValueTask<PlatformOperationResult>(cancellationToken.IsCancellationRequested
                    ? new PlatformOperationResult(PlatformOperationStatus.Cancelled, "platform.cancelled")
                    : PlatformOperationResult.Unavailable);
        }

        private sealed class NullPlatformStatsService : IPlatformStatsService
        {
            public ValueTask<PlatformOperationResult> AddAsync(ContentId statisticId, long amount, CancellationToken cancellationToken = default) =>
                new ValueTask<PlatformOperationResult>(cancellationToken.IsCancellationRequested
                    ? new PlatformOperationResult(PlatformOperationStatus.Cancelled, "platform.cancelled")
                    : PlatformOperationResult.Unavailable);
        }

        private sealed class NullCloudSyncService : ICloudSyncService
        {
            public ValueTask<CloudFileRevision> GetRemoteRevisionAsync(string slot, CancellationToken cancellationToken = default) =>
                new ValueTask<CloudFileRevision>(default(CloudFileRevision));

            public ValueTask<PlatformOperationResult> UploadAsync(string slot, ReadOnlyMemory<byte> data, CloudFileRevision localRevision, CancellationToken cancellationToken = default) =>
                new ValueTask<PlatformOperationResult>(cancellationToken.IsCancellationRequested
                    ? new PlatformOperationResult(PlatformOperationStatus.Cancelled, "platform.cancelled")
                    : PlatformOperationResult.Unavailable);

            public ValueTask<PlatformOperationResult> DownloadAsync(string slot, CancellationToken cancellationToken = default) =>
                new ValueTask<PlatformOperationResult>(cancellationToken.IsCancellationRequested
                    ? new PlatformOperationResult(PlatformOperationStatus.Cancelled, "platform.cancelled")
                    : PlatformOperationResult.Unavailable);
        }

        private sealed class NullRichPresenceService : IRichPresenceService
        {
            public ValueTask<PlatformOperationResult> SetAsync(ContentId presenceId, CancellationToken cancellationToken = default) =>
                new ValueTask<PlatformOperationResult>(cancellationToken.IsCancellationRequested
                    ? new PlatformOperationResult(PlatformOperationStatus.Cancelled, "platform.cancelled")
                    : PlatformOperationResult.Unavailable);

            public ValueTask<PlatformOperationResult> ClearAsync(CancellationToken cancellationToken = default) =>
                new ValueTask<PlatformOperationResult>(cancellationToken.IsCancellationRequested
                    ? new PlatformOperationResult(PlatformOperationStatus.Cancelled, "platform.cancelled")
                    : PlatformOperationResult.Unavailable);
        }

        private sealed class NullUserIdentityService : IUserIdentityService
        {
            public PlatformUserIdentity Current => new PlatformUserIdentity(false, "offline.local", string.Empty);
        }
    }
}
