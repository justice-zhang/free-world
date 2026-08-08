using System;
using System.Threading.Tasks;
using Game.Core;
using Game.Platform.Abstractions;

namespace Game.Application
{
    /// <summary>Routes application events to replaceable platform services.</summary>
    public sealed class PlatformApplicationEventRouter : IDisposable
    {
        private static readonly ContentId RunsCompleted = RequireId("platform.stat.runs_completed");
        private static readonly ContentId FirstCompletedRun = RequireId("platform.achievement.first_completed_run");
        private readonly ApplicationEventStream events;
        private readonly IPlatformFacade platform;
        private bool disposed;

        public PlatformApplicationEventRouter(ApplicationEventStream applicationEvents, IPlatformFacade platformFacade)
        {
            events = applicationEvents ?? throw new ArgumentNullException(nameof(applicationEvents));
            platform = platformFacade ?? throw new ArgumentNullException(nameof(platformFacade));
            events.Published += OnEvent;
        }

        public PlatformOperationResult LastOperation { get; private set; }

        /// <summary>Stops observing application events.</summary>
        public void Dispose()
        {
            disposed = true;
            events.Published -= OnEvent;
        }

        private void OnEvent(ApplicationEvent applicationEvent)
        {
            if (applicationEvent.Type != ApplicationEventType.RunCompleted &&
                applicationEvent.Type != ApplicationEventType.RunResultCommitted) return;
            var task = RouteAsync(applicationEvent).AsTask();
            if (task.IsCompletedSuccessfully) LastOperation = task.Result;
            else _ = ObserveAsync(task);
        }

        private async ValueTask<PlatformOperationResult> RouteAsync(ApplicationEvent applicationEvent)
        {
            var result = await platform.Stats.AddAsync(RunsCompleted, 1).ConfigureAwait(false);
            var victory = applicationEvent.Type == ApplicationEventType.RunResultCommitted
                ? applicationEvent.CommittedResult.IsVictory
                : string.Equals(applicationEvent.Result.ReasonKey, "ui.result.reason.completed", StringComparison.Ordinal);
            if (victory)
                result = await platform.Achievements.UnlockAsync(FirstCompletedRun).ConfigureAwait(false);
            return result;
        }

        private async Task ObserveAsync(Task<PlatformOperationResult> task)
        {
            try
            {
                var result = await task.ConfigureAwait(false);
                if (!disposed) LastOperation = result;
            }
            catch (Exception)
            {
                if (!disposed) LastOperation = new PlatformOperationResult(PlatformOperationStatus.Failed, "platform.failed");
            }
        }

        private static ContentId RequireId(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }
    }
}
