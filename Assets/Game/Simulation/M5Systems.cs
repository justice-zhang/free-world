using System;

namespace Game.Simulation
{
    /// <summary>Advances a single world-owned encounter scheduler.</summary>
    public sealed class SpawnSchedulerSystem : ISimulationSystem
    {
        public SimulationSystemId Id => SimulationSystemId.SpawnScheduler;

        public void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Encounter?.Tick(world);
        }
    }

    /// <summary>Advances all enemy behavior modules and local steering in one pass.</summary>
    public sealed class EnemyDecisionSystem : ISimulationSystem
    {
        public SimulationSystemId Id => SimulationSystemId.EnemyDecision;

        public void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Enemies.TickDecisions(world);
        }
    }
}
