using System;
using Game.Content.Runtime;
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
        private bool interactHeld;

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

        /// <summary>Copies current run truth into a reusable UI-safe projection.</summary>
        public bool CaptureUiSnapshot(RunUiSnapshot target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.Reset();
            if (HasEnded || !world.Actors.Contains(player)) return false;
            target.Tick = world.Tick;
            target.DurationSeconds = world.Tick * SimulationClock.TickDurationSeconds;
            if (world.Actors.TryReadHealth(player, out var health))
            {
                target.Health = health.Current;
                target.MaximumHealth = health.Maximum;
            }
            if (world.Actors.TryReadShield(player, out var shield))
            {
                target.Shield = shield.Current;
                target.MaximumShield = shield.Maximum;
            }
            var progression = world.Progression;
            target.Level = progression.Experience.Level;
            target.Experience = progression.Experience.CurrentExperience;
            target.RequiredExperience = progression.Experience.RequiredExperience;
            if (world.Qinglan?.Mechanics.TryGet(player, out var mechanic) == true)
            {
                target.MechanicTier = mechanic.Tier;
                target.MechanicValue = mechanic.CurrentValue;
            }

            var build = progression.Build;
            for (var index = 0; index < build.Skills.Count; index++)
            {
                var item = build.Skills.GetAt(index);
                target.AddBuild(item.ContentId.Value, item.Level, item.MaximumLevel, 1);
            }
            for (var index = 0; index < build.Passives.Count; index++)
            {
                var item = build.Passives.GetAt(index);
                target.AddBuild(item.ContentId.Value, item.Level, item.MaximumLevel, 2);
            }
            var relics = world.Qinglan?.Rewards.Relics;
            if (relics != null)
            {
                for (var index = 0; index < relics.Count; index++)
                {
                    var item = relics.GetAt(index);
                    target.AddBuild(item.RelicId.Value, item.Level, item.MaximumLevel, 3);
                }
            }
            for (var index = 0; index < build.AppliedEvolutionCount; index++)
                target.AddBuild(build.GetAppliedEvolutionAt(index).Value, 1, 1, 4);

            var map = world.Qinglan?.MapObjectives;
            if (map != null)
            {
                for (var index = 0; index < map.ObjectiveCount; index++)
                {
                    var item = map.GetObjectiveAt(index);
                    target.AddMap(item.Id.Value, 1, (byte)item.State, item.Progress);
                }
                for (var index = 0; index < map.EventCount; index++)
                {
                    var item = map.GetEventAt(index);
                    target.AddMap(item.Id.Value, 2, (byte)item.State, item.Progress);
                }
                for (var index = 0; index < map.LandmarkCount; index++)
                {
                    var item = map.GetLandmarkAt(index);
                    target.AddMap(item.Id.Value, 3, (byte)item.State, item.State == LandmarkState.Claimed ? 1f : 0f);
                }
            }

            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (!world.Enemies.TryGetSnapshot(handle, out var enemy) || !enemy.Boss ||
                    world.Qinglan?.Bosses.TryGet(handle, out var boss) != true)
                    continue;
                target.HasBoss = true;
                target.BossId = boss.BossId.Value;
                target.BossPhase = boss.Phase;
                target.BossPhaseCount = boss.PhaseCount;
                if (world.Actors.TryReadHealth(handle, out var bossHealth))
                {
                    target.BossHealth = bossHealth.Current;
                    target.BossMaximumHealth = bossHealth.Maximum;
                }
                break;
            }
            return true;
        }

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
            PrepareMapInteraction();
            var ticks = Runner.Advance(elapsedSeconds);
            AdvanceMapInteraction(ticks);
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

        /// <summary>Sets the held interaction intent; map truth validates range and state.</summary>
        public void SetInteractHeld(bool held) => interactHeld = held;

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

        private void PrepareMapInteraction()
        {
            if (!interactHeld || world.Qinglan?.MapObjectives == null ||
                !world.Actors.TryRead(player, out var playerState)) return;
            var map = world.Qinglan.MapObjectives;
            for (var index = 0; index < map.LandmarkCount; index++)
            {
                var landmark = map.GetLandmarkAt(index);
                if (landmark.State == LandmarkState.Discovered)
                    map.ClaimLandmark(landmark.Id);
            }
            var playerEntity = new SpatialEntity(EntityKind.Actor, player);
            for (var index = 0; index < map.ObjectiveCount; index++)
            {
                var objective = map.GetObjectiveAt(index);
                if (objective.State == ObjectiveState.Available)
                {
                    if (map.ActivateObjective(objective.Id, playerEntity, playerState.Position, 2.5f) == MapCommandStatus.Applied)
                        map.BeginObjectiveDefense(objective.Id);
                }
                else if (objective.State == ObjectiveState.Activating && objective.Activator == playerEntity)
                {
                    map.BeginObjectiveDefense(objective.Id);
                }
            }
        }

        private void AdvanceMapInteraction(int ticks)
        {
            if (!interactHeld || ticks <= 0 || world.Qinglan?.MapObjectives == null) return;
            var map = world.Qinglan.MapObjectives;
            var progress = ticks * (float)SimulationClock.TickDurationSeconds / 4f;
            var playerEntity = new SpatialEntity(EntityKind.Actor, player);
            for (var index = 0; index < map.ObjectiveCount; index++)
            {
                var objective = map.GetObjectiveAt(index);
                if (objective.State == ObjectiveState.Defending && objective.Activator == playerEntity)
                    map.ReportObjectiveProgress(objective.Id, progress);
            }
            for (var index = 0; index < map.EventCount; index++)
            {
                var mapEvent = map.GetEventAt(index);
                if (mapEvent.State == ObjectiveState.Defending)
                    map.ReportEventProgress(mapEvent.Id, progress);
            }
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
