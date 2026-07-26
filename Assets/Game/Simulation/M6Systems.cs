using System;

namespace Game.Simulation
{
    /// <summary>Attracts and collects M6 XP pickups without structural mutation.</summary>
    public sealed class PickupSystem : ISimulationSystem
    {
        public SimulationSystemId Id => SimulationSystemId.Pickup;

        public void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Progression?.TickPickups(world);
        }
    }

    /// <summary>Applies XP collected during the current fixed tick.</summary>
    public sealed class ExperienceSystem : ISimulationSystem
    {
        public SimulationSystemId Id => SimulationSystemId.Experience;

        public void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Progression?.ApplyPendingExperience();
        }
    }

    /// <summary>Creates one deterministic application-level level-up request.</summary>
    public sealed class LevelUpRequestSystem : ISimulationSystem
    {
        public SimulationSystemId Id => SimulationSystemId.LevelUpRequest;

        public void Execute(SimulationWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Progression?.RequestNextChoice();
        }
    }
}
