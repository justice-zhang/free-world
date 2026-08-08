using System;

namespace Game.Simulation
{
    /// <summary>
    /// Integrates velocity for each dedicated M2 store without structural mutation.
    /// </summary>
    public sealed class MovementSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.Movement;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            MoveActors(world);
            MoveProjectiles(world);
            MoveAreas(world);
            MovePickups(world);
        }

        private static SimulationEntityState Integrate(
            SimulationEntityState state,
            float deltaTime)
        {
            state.Position += state.Velocity * deltaTime;
            var isMoving = state.Velocity.X != 0f || state.Velocity.Y != 0f;
            if (isMoving)
            {
                state.FacingRadians =
                    (float)Math.Atan2(state.Velocity.Y, state.Velocity.X);
                state.StateFlags |= SimulationStateFlags.Moving;
            }
            else
            {
                state.StateFlags &= ~SimulationStateFlags.Moving;
            }

            return state;
        }

        private static void MoveActors(SimulationWorld world)
        {
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                var previous = world.Actors.GetStateAt(index);
                var state = Integrate(previous, world.DeltaTimeSeconds);
                state.Position = world.Enemies.ResolveMovement(
                    world.Map,
                    handle,
                    previous.Position,
                    state.Position);
                if (world.Qinglan != null)
                {
                    var distance = System.Numerics.Vector2.Distance(previous.Position, state.Position);
                    if (distance > 0f)
                    {
                        world.ResolvedMovements.Add(
                            new ResolvedMovement(
                                new SpatialEntity(EntityKind.Actor, handle),
                                world.MovementSources.ConsumeSource(handle),
                                distance));
                    }
                    else
                    {
                        world.MovementSources.ConsumeSource(handle);
                    }
                }
                world.Actors.SetStateAt(index, state);
                world.SpatialGrid.Update(
                    new SpatialEntity(EntityKind.Actor, handle),
                    state.Position);
            }
        }

        private static void MoveProjectiles(SimulationWorld world)
        {
            for (var index = 0; index < world.Projectiles.Count; index++)
            {
                var handle = world.Projectiles.GetHandleAt(index);
                var state = Integrate(
                    world.Projectiles.GetStateAt(index),
                    world.DeltaTimeSeconds);
                world.Projectiles.SetStateAt(index, state);
                world.SpatialGrid.Update(
                    new SpatialEntity(EntityKind.Projectile, handle),
                    state.Position);
            }
        }

        private static void MoveAreas(SimulationWorld world)
        {
            for (var index = 0; index < world.Areas.Count; index++)
            {
                var handle = world.Areas.GetHandleAt(index);
                var state = Integrate(world.Areas.GetStateAt(index), world.DeltaTimeSeconds);
                world.Areas.SetStateAt(index, state);
                world.SpatialGrid.Update(
                    new SpatialEntity(EntityKind.Area, handle),
                    state.Position);
            }
        }

        private static void MovePickups(SimulationWorld world)
        {
            for (var index = 0; index < world.Pickups.Count; index++)
            {
                var handle = world.Pickups.GetHandleAt(index);
                var state = Integrate(world.Pickups.GetStateAt(index), world.DeltaTimeSeconds);
                world.Pickups.SetStateAt(index, state);
                world.SpatialGrid.Update(
                    new SpatialEntity(EntityKind.Pickup, handle),
                    state.Position);
            }
        }
    }

    /// <summary>
    /// Advances finite lifetimes and queues removals for the cleanup system.
    /// </summary>
    public sealed class LifetimeSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.Lifetime;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            TickActors(world);
            TickProjectiles(world);
            TickAreas(world);
            TickPickups(world);
        }

        private static bool TickLifetime(
            ref SimulationEntityState state,
            float deltaTime)
        {
            if (float.IsPositiveInfinity(state.RemainingLifetimeSeconds))
            {
                return false;
            }

            state.RemainingLifetimeSeconds -= deltaTime;
            return state.RemainingLifetimeSeconds <= 0f;
        }

        private static void TickActors(SimulationWorld world)
        {
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (world.Actors.IsDeathPending(handle))
                {
                    continue;
                }

                var state = world.Actors.GetStateAt(index);
                if (TickLifetime(ref state, world.DeltaTimeSeconds))
                {
                    world.Commands.Remove(EntityKind.Actor, handle);
                }

                world.Actors.SetStateAt(index, state);
            }
        }

        private static void TickProjectiles(SimulationWorld world)
        {
            for (var index = 0; index < world.Projectiles.Count; index++)
            {
                var state = world.Projectiles.GetStateAt(index);
                if (TickLifetime(ref state, world.DeltaTimeSeconds))
                {
                    world.Commands.Remove(
                        EntityKind.Projectile,
                        world.Projectiles.GetHandleAt(index));
                }

                world.Projectiles.SetStateAt(index, state);
            }
        }

        private static void TickAreas(SimulationWorld world)
        {
            for (var index = 0; index < world.Areas.Count; index++)
            {
                var state = world.Areas.GetStateAt(index);
                if (TickLifetime(ref state, world.DeltaTimeSeconds))
                {
                    world.Commands.Remove(EntityKind.Area, world.Areas.GetHandleAt(index));
                }

                world.Areas.SetStateAt(index, state);
            }
        }

        private static void TickPickups(SimulationWorld world)
        {
            for (var index = 0; index < world.Pickups.Count; index++)
            {
                var state = world.Pickups.GetStateAt(index);
                if (TickLifetime(ref state, world.DeltaTimeSeconds))
                {
                    world.Commands.Remove(EntityKind.Pickup, world.Pickups.GetHandleAt(index));
                }

                world.Pickups.SetStateAt(index, state);
            }
        }
    }

    /// <summary>
    /// Applies buffered creates and removes after all structural readers have completed.
    /// </summary>
    public sealed class CleanupSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.Cleanup;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            var commandCount = world.Commands.Count;
            for (var index = 0; index < commandCount; index++)
            {
                var command = world.Commands.GetAt(index);
                if (command.Type == SimulationCommandType.Create)
                {
                    var handle = world.CreateEntity(
                        command.EntityKind,
                        command.InitialState);
                    world.EmitEvent(
                        SimulationEventType.Created,
                        command.EntityKind,
                        handle,
                        command.InitialState.Position);
                }
                else if (command.Type == SimulationCommandType.Remove &&
                         world.TryRemoveEntity(
                             command.EntityKind,
                             command.Target,
                             out var removedPosition))
                {
                    world.EmitEvent(
                        SimulationEventType.Removed,
                        command.EntityKind,
                        command.Target,
                        removedPosition);
                }
            }

            world.Commands.Clear();
            world.Enemies.ApplyPendingSpawns(world);
            world.Skills.ApplyPendingSpawns(world);
            world.Progression?.ApplyPendingPickups(world);
            world.Qinglan?.Rewards.ApplyPendingPickups(world);
        }
    }

    /// <summary>
    /// Produces previous/current render state after cleanup has completed.
    /// </summary>
    public sealed class SnapshotBuildSystem : ISimulationSystem
    {
        /// <inheritdoc />
        public SimulationSystemId Id => SimulationSystemId.SnapshotBuild;

        /// <inheritdoc />
        public void Execute(SimulationWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            world.BuildRenderSnapshot();
        }
    }
}
