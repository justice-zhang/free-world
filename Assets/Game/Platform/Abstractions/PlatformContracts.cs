using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Core;

namespace Game.Platform.Abstractions
{
    /// <summary>Portable outcome categories for optional platform operations.</summary>
    public enum PlatformOperationStatus : byte
    {
        Success = 0,
        Unavailable = 1,
        Failed = 2,
        Cancelled = 3
    }

    /// <summary>Platform outcome with a localizable diagnostic key.</summary>
    public readonly struct PlatformOperationResult
    {
        public PlatformOperationResult(PlatformOperationStatus status, string diagnosticKey = "")
        {
            Status = status;
            DiagnosticKey = diagnosticKey ?? string.Empty;
        }

        public PlatformOperationStatus Status { get; }
        public string DiagnosticKey { get; }
        public bool IsSuccess => Status == PlatformOperationStatus.Success;
        public static PlatformOperationResult Unavailable => new PlatformOperationResult(PlatformOperationStatus.Unavailable, "platform.unavailable");
    }

    /// <summary>Platform-neutral current user identity.</summary>
    public readonly struct PlatformUserIdentity
    {
        public PlatformUserIdentity(bool signedIn, string userId, string displayName)
        {
            IsSignedIn = signedIn;
            UserId = userId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }
        public bool IsSignedIn { get; }
        public string UserId { get; }
        public string DisplayName { get; }
    }

    /// <summary>Comparable metadata for one local or remote cloud file.</summary>
    public readonly struct CloudFileRevision
    {
        public CloudFileRevision(bool exists, string checksum, string lastWriteUtc, string deviceId, long generation)
        {
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            Exists = exists;
            Checksum = checksum ?? string.Empty;
            LastWriteUtc = lastWriteUtc ?? string.Empty;
            DeviceId = deviceId ?? string.Empty;
            Generation = generation;
        }
        public bool Exists { get; }
        public string Checksum { get; }
        public string LastWriteUtc { get; }
        public string DeviceId { get; }
        public long Generation { get; }
    }

    /// <summary>Local, remote, and last-synchronized revisions used for conflict resolution.</summary>
    public readonly struct CloudSyncState
    {
        public CloudSyncState(CloudFileRevision local, CloudFileRevision remote, string lastSynchronizedChecksum)
        {
            Local = local;
            Remote = remote;
            LastSynchronizedChecksum = lastSynchronizedChecksum ?? string.Empty;
        }
        public CloudFileRevision Local { get; }
        public CloudFileRevision Remote { get; }
        public string LastSynchronizedChecksum { get; }
    }

    /// <summary>Classification of the relationship between local and remote files.</summary>
    public enum CloudConflictKind : byte
    {
        Synchronized = 0,
        LocalNewer = 1,
        RemoteNewer = 2,
        Diverged = 3,
        LocalOnly = 4,
        RemoteOnly = 5,
        NoFiles = 6
    }

    /// <summary>Safe next action selected by a cloud conflict strategy.</summary>
    public enum CloudConflictDecision : byte
    {
        NoAction = 0,
        UploadLocal = 1,
        DownloadRemote = 2,
        RequireUserChoice = 3
    }

    /// <summary>Conflict classification and the selected safe action.</summary>
    public readonly struct CloudConflictResolution
    {
        public CloudConflictResolution(CloudConflictKind kind, CloudConflictDecision decision)
        {
            Kind = kind;
            Decision = decision;
        }
        public CloudConflictKind Kind { get; }
        public CloudConflictDecision Decision { get; }
    }

    /// <summary>Replaceable deterministic cloud conflict policy.</summary>
    public interface ICloudConflictStrategy
    {
        CloudConflictResolution Resolve(in CloudSyncState state);
    }

    /// <summary>Conservative conflict policy that never silently overwrites a diverged save.</summary>
    public sealed class ConservativeCloudConflictStrategy : ICloudConflictStrategy
    {
        public CloudConflictResolution Resolve(in CloudSyncState state)
        {
            var local = state.Local;
            var remote = state.Remote;
            if (!local.Exists && !remote.Exists) return Result(CloudConflictKind.NoFiles, CloudConflictDecision.NoAction);
            if (local.Exists && !remote.Exists) return Result(CloudConflictKind.LocalOnly, CloudConflictDecision.UploadLocal);
            if (!local.Exists) return Result(CloudConflictKind.RemoteOnly, CloudConflictDecision.DownloadRemote);
            if (string.Equals(local.Checksum, remote.Checksum, StringComparison.Ordinal))
                return Result(CloudConflictKind.Synchronized, CloudConflictDecision.NoAction);

            var baseline = state.LastSynchronizedChecksum;
            if (!string.IsNullOrEmpty(baseline))
            {
                if (string.Equals(remote.Checksum, baseline, StringComparison.Ordinal))
                    return Result(CloudConflictKind.LocalNewer, CloudConflictDecision.UploadLocal);
                if (string.Equals(local.Checksum, baseline, StringComparison.Ordinal))
                    return Result(CloudConflictKind.RemoteNewer, CloudConflictDecision.DownloadRemote);
            }
            return Result(CloudConflictKind.Diverged, CloudConflictDecision.RequireUserChoice);
        }

        private static CloudConflictResolution Result(CloudConflictKind kind, CloudConflictDecision decision) =>
            new CloudConflictResolution(kind, decision);
    }

    /// <summary>Optional platform achievement operations.</summary>
    public interface IAchievementService
    {
        ValueTask<PlatformOperationResult> UnlockAsync(ContentId achievementId, CancellationToken cancellationToken = default);
    }

    /// <summary>Optional platform statistics operations.</summary>
    public interface IPlatformStatsService
    {
        ValueTask<PlatformOperationResult> AddAsync(ContentId statisticId, long amount, CancellationToken cancellationToken = default);
    }

    /// <summary>Optional cloud revision and transfer operations.</summary>
    public interface ICloudSyncService
    {
        ValueTask<CloudFileRevision> GetRemoteRevisionAsync(string slot, CancellationToken cancellationToken = default);
        ValueTask<PlatformOperationResult> UploadAsync(string slot, ReadOnlyMemory<byte> data, CloudFileRevision localRevision, CancellationToken cancellationToken = default);
        ValueTask<PlatformOperationResult> DownloadAsync(string slot, CancellationToken cancellationToken = default);
    }

    /// <summary>Optional rich-presence operations keyed by stable IDs.</summary>
    public interface IRichPresenceService
    {
        ValueTask<PlatformOperationResult> SetAsync(ContentId presenceId, CancellationToken cancellationToken = default);
        ValueTask<PlatformOperationResult> ClearAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Read-only platform identity boundary.</summary>
    public interface IUserIdentityService
    {
        PlatformUserIdentity Current { get; }
    }

    /// <summary>Composition root for replaceable platform sub-services.</summary>
    public interface IPlatformFacade
    {
        bool IsAvailable { get; }
        IAchievementService Achievements { get; }
        IPlatformStatsService Stats { get; }
        ICloudSyncService Cloud { get; }
        IRichPresenceService RichPresence { get; }
        IUserIdentityService Identity { get; }
    }
}
