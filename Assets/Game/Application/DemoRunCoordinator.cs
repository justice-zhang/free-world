using System;
using Game.Core;

namespace Game.Application
{
    /// <summary>Low-frequency Demo page/lifecycle stage without expanding the legacy GameState enum.</summary>
    public enum DemoFlowStage : byte
    {
        Title = 1,
        CharacterSelect = 2,
        MapSelect = 3,
        Preparing = 4,
        Active = 5,
        UpgradePaused = 6,
        RewardPaused = 7,
        UserPaused = 8,
        Ending = 9,
        Result = 10,
        Hub = 11,
        ContentError = 12,
        Disposed = 13
    }

    /// <summary>Run-owned resources returned by a composition-root factory.</summary>
    public interface IRunSessionHandle : IDisposable
    {
        RunSession Session { get; }
        bool IsDisposed { get; }
    }

    /// <summary>Stable-ID run assembly boundary implemented outside Application.</summary>
    public interface IRunSessionFactory
    {
        Result<IRunSessionHandle> Create(RunDescriptor descriptor, GameStateMachine stateMachine);
    }

    /// <summary>
    /// Owns the Qinglan Title-to-Hub lifecycle. It freezes results but deliberately
    /// does not persist Profile, clear Recovery, or publish platform completion.
    /// </summary>
    public sealed class DemoRunCoordinator : IDisposable
    {
        private readonly GameStateMachine stateMachine;
        private readonly IRunSessionFactory factory;
        private readonly bool requireCommittedResult;
        private RunDescriptor pendingDescriptor;
        private IRunSessionHandle handle;
        private bool hasResult;
        private bool resultCommitted;

        public DemoRunCoordinator(GameStateMachine gameStateMachine, IRunSessionFactory runFactory)
            : this(gameStateMachine, runFactory, false)
        {
        }

        /// <summary>Creates a flow that can optionally block page transitions until durable settlement.</summary>
        public DemoRunCoordinator(
            GameStateMachine gameStateMachine,
            IRunSessionFactory runFactory,
            bool requireDurableResultCommit)
        {
            stateMachine = gameStateMachine ?? throw new ArgumentNullException(nameof(gameStateMachine));
            factory = runFactory ?? throw new ArgumentNullException(nameof(runFactory));
            requireCommittedResult = requireDurableResultCommit;
            stateMachine.EnterMainMenu();
            Stage = DemoFlowStage.Title;
        }

        public DemoFlowStage Stage { get; private set; }
        public GameState CurrentState => stateMachine.CurrentState;
        public RunSession Session => handle?.Session;
        public bool HasResult => hasResult;
        public bool HasUncommittedResult => hasResult && !resultCommitted;
        public RunResult LatestResult { get; private set; }
        public string ContentErrorKey { get; private set; } = string.Empty;
        public Error LastError { get; private set; }

        public bool ShowCharacterSelect()
        {
            if (Stage != DemoFlowStage.Title && Stage != DemoFlowStage.MapSelect) return false;
            stateMachine.EnterCharacterSelect();
            Stage = DemoFlowStage.CharacterSelect;
            return true;
        }

        public bool ShowMapSelect()
        {
            if (Stage != DemoFlowStage.CharacterSelect) return false;
            stateMachine.EnterMapSelect();
            Stage = DemoFlowStage.MapSelect;
            return true;
        }

        public bool BeginRun(RunDescriptor descriptor)
        {
            if (Stage != DemoFlowStage.MapSelect || descriptor == null || handle != null) return false;
            pendingDescriptor = descriptor;
            stateMachine.EnterLoading();
            Stage = DemoFlowStage.Preparing;
            return true;
        }

        public int Tick(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (Stage == DemoFlowStage.Disposed || Stage == DemoFlowStage.ContentError ||
                Stage == DemoFlowStage.Title || Stage == DemoFlowStage.CharacterSelect ||
                Stage == DemoFlowStage.MapSelect || Stage == DemoFlowStage.Result ||
                Stage == DemoFlowStage.Hub)
                return 0;
            if (Stage == DemoFlowStage.Preparing)
            {
                CompletePreparation();
                return 0;
            }
            if (Stage == DemoFlowStage.Ending)
            {
                CompleteEnding();
                return 0;
            }
            if (Stage != DemoFlowStage.Active) return 0;

            var ticks = Session.Advance(elapsedSeconds);
            SynchronizeSessionState();
            return ticks;
        }

        public bool Pause()
        {
            if (Stage != DemoFlowStage.Active || Session == null || !Session.Pause()) return false;
            Stage = DemoFlowStage.UserPaused;
            return true;
        }

        public bool Resume()
        {
            if (Stage != DemoFlowStage.UserPaused || Session == null || !Session.Resume()) return false;
            Stage = DemoFlowStage.Active;
            return true;
        }

        public bool SelectUpgrade(int index)
        {
            if (Stage != DemoFlowStage.UpgradePaused || Session == null || !Session.SelectAt(index)) return false;
            Stage = DemoFlowStage.Active;
            return true;
        }

        public bool SkipUpgrade()
        {
            if (Stage != DemoFlowStage.UpgradePaused || Session == null || !Session.Skip()) return false;
            Stage = DemoFlowStage.Active;
            return true;
        }

        public bool SelectReward(int index)
        {
            if (Stage != DemoFlowStage.RewardPaused || Session == null || !Session.SelectRewardAt(index)) return false;
            Stage = DemoFlowStage.Active;
            return true;
        }

        public bool EndRun(RunEndReason reason)
        {
            if ((Stage != DemoFlowStage.Active && Stage != DemoFlowStage.UserPaused &&
                 Stage != DemoFlowStage.UpgradePaused && Stage != DemoFlowStage.RewardPaused) ||
                Session == null || !Session.End(reason))
                return false;
            Stage = DemoFlowStage.Ending;
            return true;
        }

        public bool RejectRecovery(RunDescriptor descriptor)
        {
            if (Stage != DemoFlowStage.Title || descriptor == null || handle != null) return false;
            LatestResult = RunResult.RecoveryRejected(descriptor);
            hasResult = true;
            resultCommitted = false;
            stateMachine.EnterRunResult();
            Stage = DemoFlowStage.Ending;
            return true;
        }

        public bool ContinueToHub()
        {
            if (Stage != DemoFlowStage.Result ||
                (requireCommittedResult && HasUncommittedResult)) return false;
            ReleaseRun();
            stateMachine.EnterMainMenu();
            Stage = DemoFlowStage.Hub;
            return true;
        }

        /// <summary>Marks the frozen result handled only after durable save/Recovery cleanup.</summary>
        public bool ConfirmResultCommitted(ContentId transactionId)
        {
            if (Stage != DemoFlowStage.Result || !hasResult || !transactionId.IsValid ||
                LatestResult.Delta == null || LatestResult.Delta.TransactionId != transactionId)
                return false;
            resultCommitted = true;
            return true;
        }

        public bool StartAgain()
        {
            if (Stage != DemoFlowStage.Hub) return false;
            ClearResult();
            stateMachine.EnterCharacterSelect();
            Stage = DemoFlowStage.CharacterSelect;
            return true;
        }

        public bool ReturnToTitle()
        {
            if (Stage != DemoFlowStage.ContentError && Stage != DemoFlowStage.Hub) return false;
            ReleaseRun();
            pendingDescriptor = null;
            ClearResult();
            LastError = default;
            ContentErrorKey = string.Empty;
            stateMachine.EnterMainMenu();
            Stage = DemoFlowStage.Title;
            return true;
        }

        public void Dispose()
        {
            if (Stage == DemoFlowStage.Disposed) return;
            ReleaseRun();
            pendingDescriptor = null;
            Stage = DemoFlowStage.Disposed;
        }

        private void CompletePreparation()
        {
            var created = factory.Create(pendingDescriptor, stateMachine);
            pendingDescriptor = null;
            if (!created.IsSuccess)
            {
                LastError = created.Error;
                ContentErrorKey = "ui.content_error.run_assembly";
                stateMachine.EnterContentError();
                Stage = DemoFlowStage.ContentError;
                return;
            }
            handle = created.Value;
            Stage = DemoFlowStage.Active;
        }

        private void SynchronizeSessionState()
        {
            if (Session.HasEnded)
            {
                Stage = DemoFlowStage.Ending;
                return;
            }
            switch (Session.StateMachine.CurrentState)
            {
                case GameState.InRun:
                    Stage = DemoFlowStage.Active;
                    break;
                case GameState.LevelUpChoice:
                    Stage = DemoFlowStage.UpgradePaused;
                    break;
                case GameState.RewardChoice:
                    Stage = DemoFlowStage.RewardPaused;
                    break;
                case GameState.Pause:
                    Stage = DemoFlowStage.UserPaused;
                    break;
            }
        }

        private void CompleteEnding()
        {
            if (!hasResult)
            {
                if (Session == null || !Session.HasEnded)
                    throw new InvalidOperationException("Ending requires a frozen run result.");
                LatestResult = Session.Result;
                hasResult = true;
                resultCommitted = false;
            }
            Stage = DemoFlowStage.Result;
        }

        private void ReleaseRun()
        {
            handle?.Dispose();
            handle = null;
        }

        private void ClearResult()
        {
            LatestResult = default;
            hasResult = false;
            resultCommitted = false;
        }
    }
}
