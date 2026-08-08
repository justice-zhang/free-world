using System;
using Game.Core;
using Game.Simulation;

namespace Game.Application
{
    /// <summary>
    /// Application-owned M6 run state. It translates presentation time and level-up
    /// commands while keeping candidate rules in Simulation.
    /// </summary>
    public sealed class RunSession
    {
        private readonly SimulationWorld world;
        private readonly EntityHandle player;
        private readonly RunDescriptor descriptor;
        private RewardChoice currentRewardChoice;

        public RunSession(
            SimulationWorld simulationWorld,
            EntityHandle playerActor,
            GameStateMachine stateMachine,
            SimulationClock clock = null)
            : this(
                simulationWorld,
                playerActor,
                stateMachine,
                RunDescriptor.CreateLegacy(),
                clock)
        {
        }

        public RunSession(
            SimulationWorld simulationWorld,
            EntityHandle playerActor,
            GameStateMachine stateMachine,
            RunDescriptor runDescriptor,
            SimulationClock clock = null)
        {
            world = simulationWorld ?? throw new ArgumentNullException(nameof(simulationWorld));
            StateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            if (world.Progression == null) throw new ArgumentException("World progression must be initialized.", nameof(simulationWorld));
            if (!world.Actors.Contains(playerActor)) throw new ArgumentException("Player must be live.", nameof(playerActor));
            player = playerActor;
            descriptor = runDescriptor ?? throw new ArgumentNullException(nameof(runDescriptor));
            Runner = new FixedTickRunner(world, clock);
            StateMachine.EnterRun();
        }

        public FixedTickRunner Runner { get; }
        public RunDescriptor Descriptor => descriptor;
        public GameStateMachine StateMachine { get; }
        public UpgradeOfferSet CurrentOffers => world.Progression.CurrentOffers;
        public RewardChoice CurrentRewardChoice => currentRewardChoice;
        public RenderSnapshot RenderSnapshot => world.RenderSnapshot;
        public SimulationEventBuffer SimulationEvents => world.Events;
        public CombatEventBuffer CombatEvents => world.CombatEvents;
        public float InterpolationAlpha => (float)Runner.Clock.InterpolationAlpha;
        public SpatialEntity Player => new SpatialEntity(EntityKind.Actor, player);
        public bool HasEnded { get; private set; }
        public RunResult Result { get; private set; }

        /// <summary>Resolves optional stable presentation identity without exposing stores.</summary>
        public bool TryGetVisualProfileId(SpatialEntity entity, out ContentId profileId)
        {
            if (entity.Kind == EntityKind.Actor &&
                world.Enemies.TryGetSnapshot(entity.Handle, out var enemy) &&
                world.Enemies.Catalog.TryGet(enemy.EnemyId, out var definition))
            {
                profileId = definition.Source.VisualProfileId;
                return profileId.IsValid;
            }

            profileId = default;
            return false;
        }

        public int Advance(double elapsedSeconds)
        {
            if (HasEnded) return 0;
            if (StateMachine.CurrentState == GameState.LevelUpChoice ||
                StateMachine.CurrentState == GameState.RewardChoice) return 0;
            var ticks = Runner.Advance(elapsedSeconds);
            if (!world.Actors.Contains(player))
            {
                End(RunEndReason.PlayerDefeated);
            }
            else if (world.Qinglan?.Rewards.HasPendingRelicChoice == true)
            {
                currentRewardChoice = Project(world.Qinglan.Rewards.CurrentRelicChoice);
                StateMachine.EnterRewardChoice();
            }
            else if (world.Progression.RewardChoices.HasPendingChoice)
            {
                currentRewardChoice = Project(world.Progression.RewardChoices.CurrentChoice);
                StateMachine.EnterRewardChoice();
            }
            else if (world.Progression.HasPendingChoice)
            {
                StateMachine.EnterLevelUpChoice();
            }
            else if (HasSatisfiedVictoryCondition())
            {
                End(RunEndReason.Completed);
            }
            return ticks;
        }

        /// <summary>Applies one normalized movement command at the application boundary.</summary>
        public bool SetMoveDirection(System.Numerics.Vector2 direction)
        {
            if (HasEnded || !world.Actors.TryRead(player, out var state)) return false;
            if (float.IsNaN(direction.X) || float.IsInfinity(direction.X) ||
                float.IsNaN(direction.Y) || float.IsInfinity(direction.Y)) return false;
            var lengthSquared = direction.LengthSquared();
            if (lengthSquared > 1f) direction = System.Numerics.Vector2.Normalize(direction);
            if (!world.Actors.TryReadStat(player, BuiltInStatIndices.MoveSpeed, out var moveSpeed))
                moveSpeed = 0f;
            state.Velocity = direction * moveSpeed;
            if (!world.Actors.TryWrite(player, state)) return false;
            return world.MovementSources.SetSource(player, MovementSource.PlayerCommand);
        }

        /// <summary>Pauses an active run without allowing presentation time to accumulate.</summary>
        public bool Pause()
        {
            if (HasEnded || StateMachine.CurrentState != GameState.InRun) return false;
            Runner.Clock.Pause();
            StateMachine.EnterPause();
            return true;
        }

        /// <summary>Resumes a player-paused run.</summary>
        public bool Resume()
        {
            if (HasEnded || StateMachine.CurrentState != GameState.Pause) return false;
            Runner.Clock.Resume();
            StateMachine.EnterRun();
            return true;
        }

        /// <summary>Debug-map command used to exercise the real level-up flow.</summary>
        public bool GrantDebugExperience(float amount)
        {
            if (HasEnded || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
                return false;
            world.Progression.Experience.Gain(amount);
            return true;
        }

        public bool Select(ContentId offerId) => Resolve(() => world.Progression.SelectOffer(offerId));

        public bool SelectAt(int index)
        {
            var offers = CurrentOffers;
            if (offers == null || index < 0 || index >= offers.Count) return false;
            return Select(offers.GetAt(index).Source.Id);
        }

        public bool Skip() => Resolve(world.Progression.SkipOffer);

        public bool Reroll()
        {
            return !HasEnded && StateMachine.CurrentState == GameState.LevelUpChoice &&
                   world.Progression.RerollOffers();
        }

        public bool Banish(ContentId offerId)
        {
            return !HasEnded && StateMachine.CurrentState == GameState.LevelUpChoice &&
                   world.Progression.BanishOffer(offerId);
        }

        public bool SelectReward(ContentId offerId)
        {
            if (HasEnded || StateMachine.CurrentState != GameState.RewardChoice)
                return false;
            var committed = world.Qinglan?.Rewards.HasPendingRelicChoice == true
                ? world.Qinglan.Rewards.SelectRelic(world, offerId) == RelicChoiceResolutionStatus.Committed
                : world.Progression.RewardChoices.Select(offerId) == RewardChoiceResolutionStatus.Committed;
            if (!committed) return false;
            currentRewardChoice = null;
            Runner.Clock.Resume();
            StateMachine.EnterRun();
            return true;
        }

        public bool SelectRewardAt(int index)
        {
            var choice = currentRewardChoice;
            if (choice == null || index < 0 || index >= choice.CandidateIds.Count) return false;
            return SelectReward(choice.CandidateIds[index]);
        }

        public bool End(RunEndReason reason)
        {
            if (HasEnded || reason < RunEndReason.Completed || reason > RunEndReason.Abandoned)
                return false;
            Runner.Clock.Pause();
            Result = RunResultBuilder.Build(world, descriptor, reason);
            HasEnded = true;
            StateMachine.EnterRunResult();
            return true;
        }

        private bool Resolve(Func<bool> command)
        {
            if (HasEnded || StateMachine.CurrentState != GameState.LevelUpChoice || !command()) return false;
            Runner.Clock.Resume();
            StateMachine.EnterRun();
            return true;
        }

        private bool HasSatisfiedVictoryCondition()
        {
            if (descriptor.RequiredBossDefeats <= 0 ||
                world.Progression.Statistics.BossDefeats < descriptor.RequiredBossDefeats)
                return false;
            var rewards = world.Qinglan?.Rewards;
            if (rewards == null || !descriptor.VictoryBossId.IsValid) return true;
            return rewards.HasCommitted(
                new RewardTransactionId(descriptor.RunId, descriptor.VictoryBossId, 0));
        }

        private static RewardChoice Project(RewardChoiceSnapshot source)
        {
            if (source == null) return null;
            var candidates = new ContentId[source.CandidateCount];
            for (var index = 0; index < candidates.Length; index++)
                candidates[index] = source.GetCandidateAt(index);
            return new RewardChoice(
                source.Transaction.RunId,
                source.Transaction.SourceStableId,
                source.Transaction.Sequence,
                candidates,
                source.FallbackId);
        }

        private static RewardChoice Project(RelicChoiceSnapshot source)
        {
            if (source == null) return null;
            var candidates = new ContentId[source.CandidateCount];
            for (var index = 0; index < candidates.Length; index++)
                candidates[index] = source.GetCandidateAt(index);
            return new RewardChoice(
                source.Transaction.RunId,
                source.Transaction.SourceStableId,
                source.Transaction.Sequence,
                candidates,
                source.FallbackId);
        }
    }
}
