using System;
using Game.Core;
using Game.Simulation;

namespace Game.Application
{
    /// <summary>Terminal reasons recorded in an M6 run result.</summary>
    public enum RunEndReason : byte
    {
        Completed = 1,
        PlayerDefeated = 2,
        Abandoned = 3
    }

    /// <summary>Immutable run result assembled without presentation dependencies.</summary>
    public readonly struct RunResult
    {
        internal RunResult(
            RunEndReason reason,
            long completedTicks,
            int level,
            int skillCount,
            int passiveCount,
            int activeSynergyCount,
            in RunStatisticsSnapshot statistics)
        {
            Reason = reason;
            CompletedTicks = completedTicks;
            DurationSeconds = completedTicks * SimulationClock.TickDurationSeconds;
            Level = level;
            SkillCount = skillCount;
            PassiveCount = passiveCount;
            ActiveSynergyCount = activeSynergyCount;
            Statistics = statistics;
        }

        public RunEndReason Reason { get; }
        public long CompletedTicks { get; }
        public double DurationSeconds { get; }
        public int Level { get; }
        public int SkillCount { get; }
        public int PassiveCount { get; }
        public int ActiveSynergyCount { get; }
        public RunStatisticsSnapshot Statistics { get; }
    }

    /// <summary>
    /// Application-owned M6 run state. It translates presentation time and level-up
    /// commands while keeping candidate rules in Simulation.
    /// </summary>
    public sealed class RunSession
    {
        private readonly SimulationWorld world;
        private readonly EntityHandle player;

        public RunSession(
            SimulationWorld simulationWorld,
            EntityHandle playerActor,
            GameStateMachine stateMachine,
            SimulationClock clock = null)
        {
            world = simulationWorld ?? throw new ArgumentNullException(nameof(simulationWorld));
            StateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            if (world.Progression == null) throw new ArgumentException("World progression must be initialized.", nameof(simulationWorld));
            if (!world.Actors.Contains(playerActor)) throw new ArgumentException("Player must be live.", nameof(playerActor));
            player = playerActor;
            Runner = new FixedTickRunner(world, clock);
            StateMachine.EnterRun();
        }

        public FixedTickRunner Runner { get; }
        public GameStateMachine StateMachine { get; }
        public UpgradeOfferSet CurrentOffers => world.Progression.CurrentOffers;
        public bool HasEnded { get; private set; }
        public RunResult Result { get; private set; }

        public int Advance(double elapsedSeconds)
        {
            if (HasEnded) return 0;
            if (StateMachine.CurrentState == GameState.LevelUpChoice) return 0;
            var ticks = Runner.Advance(elapsedSeconds);
            if (!world.Actors.Contains(player))
            {
                End(RunEndReason.PlayerDefeated);
            }
            else if (world.Progression.HasPendingChoice)
            {
                StateMachine.EnterLevelUpChoice();
            }
            return ticks;
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

        public bool End(RunEndReason reason)
        {
            if (HasEnded) return false;
            Runner.Clock.Pause();
            var progression = world.Progression;
            Result = new RunResult(
                reason,
                world.Tick,
                progression.Experience.Level,
                progression.Build.Skills.Count,
                progression.Build.Passives.Count,
                progression.Build.ActiveSynergyCount,
                progression.Statistics);
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
    }
}
