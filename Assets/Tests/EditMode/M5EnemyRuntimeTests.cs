using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class M5EnemyRuntimeTests
    {
        [Test]
        public void ConfiguredBehaviorModulesEnterExpectedStates()
        {
            AssertState(EnemyKind.Chase, new Vector2(10f, 0f), EnemyBehaviorState.Pursuing, 1);
            AssertState(EnemyKind.KeepDistance, new Vector2(4f, 0f), EnemyBehaviorState.HoldingRange, 1);
            AssertState(EnemyKind.Ranged, new Vector2(5f, 0f), EnemyBehaviorState.RangedAttack, 1);
            AssertState(EnemyKind.Charge, new Vector2(3f, 0f), EnemyBehaviorState.ChargeWindup, 1);
            AssertState(EnemyKind.Charge, new Vector2(3f, 0f), EnemyBehaviorState.Charging, 5);
        }

        [Test]
        public void CoincidentEnemiesUseFiniteSeparationAndSteering()
        {
            var fixture = M5TestFactory.Create();
            var world = M5TestFactory.World(fixture, fixture.FiniteMap, 21UL, false);
            M5TestFactory.Spawn(world, fixture.Registry, fixture.Chase, new Vector2(5f, 0f));
            M5TestFactory.Spawn(world, fixture.Registry, fixture.Chase, new Vector2(5f, 0f));
            var runner = new FixedTickRunner(world);

            for (var tick = 0; tick < 30; tick++)
                runner.Advance(SimulationClock.TickDurationSeconds);

            for (var index = 0; index < world.Actors.Count; index++)
            {
                var state = world.Actors.GetStateAt(index);
                Assert.That(float.IsNaN(state.Position.X) || float.IsInfinity(state.Position.X), Is.False);
                Assert.That(float.IsNaN(state.Position.Y) || float.IsInfinity(state.Position.Y), Is.False);
                Assert.That(float.IsNaN(state.Velocity.X) || float.IsInfinity(state.Velocity.X), Is.False);
                Assert.That(float.IsNaN(state.Velocity.Y) || float.IsInfinity(state.Velocity.Y), Is.False);
            }
        }

        [Test]
        public void SameM4SkillRuntimeCanBeOwnedByPlayerAndEnemy()
        {
            var fixture = M5TestFactory.Create();
            var world = M5TestFactory.World(fixture, fixture.FiniteMap, 31UL, false);
            var enemy = M5TestFactory.Spawn(
                world,
                fixture.Registry,
                fixture.Chase,
                new Vector2(2f, 0f));
            var skillIndex = SkillTestFactory.IndexOf(fixture.Registry, fixture.Skill.Id);
            var playerSkill = world.Skills.AddInstance(
                new SpatialEntity(EntityKind.Actor, world.Enemies.Player),
                skillIndex);
            Assert.That(playerSkill.IsSuccess, Is.True, playerSkill.Error.ToString());

            new FixedTickRunner(world).Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.Actors.TryReadHealth(world.Enemies.Player, out var playerHealth), Is.True);
            Assert.That(world.Actors.TryReadHealth(enemy, out var enemyHealth), Is.True);
            Assert.That(playerHealth.Current, Is.LessThan(1_000_000f));
            Assert.That(enemyHealth.Current, Is.LessThan(fixture.Chase.BaseMaxHealth));
        }

        private static void AssertState(
            EnemyKind kind,
            Vector2 position,
            EnemyBehaviorState expected,
            int ticks)
        {
            var fixture = M5TestFactory.Create();
            var world = M5TestFactory.World(fixture, fixture.FiniteMap, 13UL, false);
            var definition = kind == EnemyKind.Chase
                ? fixture.Chase
                : kind == EnemyKind.KeepDistance
                    ? fixture.KeepDistance
                    : kind == EnemyKind.Charge
                        ? fixture.Charge
                        : fixture.Ranged;
            var enemy = M5TestFactory.Spawn(world, fixture.Registry, definition, position);
            var runner = new FixedTickRunner(world);
            for (var tick = 0; tick < ticks; tick++)
                runner.Advance(SimulationClock.TickDurationSeconds);

            Assert.That(world.Enemies.TryGetSnapshot(enemy, out var snapshot), Is.True);
            Assert.That(snapshot.BehaviorState, Is.EqualTo(expected));
        }

        private enum EnemyKind
        {
            Chase,
            KeepDistance,
            Charge,
            Ranged
        }
    }
}
