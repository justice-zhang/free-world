using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Simulation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Vector2 = System.Numerics.Vector2;

namespace Game.Editor
{
    /// <summary>
    /// Runs the real Qinglan composition root for at least twelve simulated minutes.
    /// The deterministic driver supplies movement and choices but never replaces the
    /// production factory, RunSession, map, encounter, progression, reward, or Boss runtimes.
    /// </summary>
    public static class QinglanG28VerticalSliceCommand
    {
        private const int MinimumRunTicks = 12 * 60 * SimulationClock.TickRate;
        private const int MaximumRunTicks = MinimumRunTicks + (45 * SimulationClock.TickRate);
        private const ulong PrimarySeed = 0x4732385645525441UL;
        private const ulong MobilitySeed = 0x4732384D4F42494CUL;
        private const ulong FieldSeed = 0x4732384649454C44UL;
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);
        private static readonly ContentId DriverSourceId = Id("diagnostic.g2_8.vertical_slice_driver");
        private static readonly ContentId GuideObjectiveId = Id("qinglan.objective.wind_altar.guide");
        private static readonly ContentId ListenObjectiveId = Id("qinglan.objective.wind_altar.listen");
        private static readonly ContentId StopObjectiveId = Id("qinglan.objective.wind_altar.stop_balance");
        private static readonly ContentId ZhezhiBossId = Id("qinglan.boss.zhezhi");
        private static readonly ContentId TingfengBossId = Id("qinglan.boss.tingfeng");

        public static void Run()
        {
            var exitCode = 0;
            try
            {
                var catalogs = ContentEditorCatalog.BakeAll();
                if (!catalogs.IsSuccess) throw new InvalidOperationException(catalogs.Error.ToString());
                var application = CreateApplication(catalogs.Value);
                var fairness = RunSpawnFairness(application.ContentRegistry, PrimarySeed);
                var primary = Execute(application, PrimarySeed, 2);
                application = CreateApplication(catalogs.Value);
                var repeated = Execute(application, PrimarySeed, 2);
                application = CreateApplication(catalogs.Value);
                var mobility = Execute(application, MobilitySeed, 0);
                application = CreateApplication(catalogs.Value);
                var field = Execute(application, FieldSeed, 1);

                var deterministic = Equivalent(primary, repeated);
                var routesDistinct = primary.decisionChecksum != mobility.decisionChecksum &&
                                     primary.decisionChecksum != field.decisionChecksum &&
                                     mobility.decisionChecksum != field.decisionChecksum;
                var passed = fairness.passed && deterministic && routesDistinct &&
                             Valid(primary) && Valid(repeated) && Valid(mobility) && Valid(field);
                var report = new QinglanG28VerticalSliceReport
                {
                    schemaVersion = 1,
                    status = passed ? "PASS" : "FAIL",
                    failureReason = passed ? string.Empty :
                        "One or more vertical-slice, determinism, route, or spawn-fairness gates failed.",
                    generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    unityVersion = UnityEngine.Application.unityVersion,
                    minimumRequiredTicks = MinimumRunTicks,
                    deterministicReplay = deterministic,
                    threeBuildRoutesDistinct = routesDistinct,
                    spawnFairness = fairness,
                    primary = primary,
                    repeated = repeated,
                    mobilityRoute = mobility,
                    fieldRoute = field
                };
                var output = ResolveOutputPath();
                var directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(output, JsonUtility.ToJson(report, true) + "\n");
                if (passed) Debug.Log("[Qinglan G2.8 Vertical Slice] PASS: " + output);
                else Debug.LogError("[Qinglan G2.8 Vertical Slice] FAIL: " + output);
                exitCode = passed ? 0 : 2;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        public static QinglanG28SliceSummary Execute(
            GameApplication application,
            ulong seed,
            int route)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (route < 0 || route > 2) throw new ArgumentOutOfRangeException(nameof(route));
            var factory = new QinglanDemoRunFactory(application);
            var descriptor = factory.CreateDescriptor(seed ^ 0x52554E4944473238UL, seed);
            if (!descriptor.IsSuccess) throw new InvalidOperationException(descriptor.Error.ToString());
            var created = factory.Create(descriptor.Value, application.StateMachine);
            if (!created.IsSuccess) throw new InvalidOperationException(created.Error.ToString());
            var handle = created.Value as QinglanDemoRunHandle;
            if (handle == null) throw new InvalidOperationException("Qinglan factory returned an unexpected handle.");
            var session = handle.Session;
            var world = handle.World;
            var player = session.Player.Handle;
            ApplyDriverSafety(world, player);

            var phaseMaskZhezhi = 0;
            var phaseMaskTingfeng = 0;
            var tingfengRuleMask = 0;
            var peakEnemies = 0;
            var maximumViews = 0;
            var positionsWalkable = true;
            var selections = 0;
            var rewards = 0;
            var movementDistance = 0d;
            var navigationCorrections = 0;
            var previousPlayerPosition = Vector2.Zero;
            var hasPreviousPosition = false;
            var targetKey = string.Empty;
            var targetDistance = float.MaxValue;
            var targetStallTicks = 0;

            try
            {
                while (world.Tick < MaximumRunTicks &&
                       (world.Tick < MinimumRunTicks || !session.HasEnded))
                {
                    world.Actors.TryApplyHealing(player, 1_000_000f);
                    if (TryChooseTarget(application.ContentRegistry, world, route,
                            out var target, out var selectedKey))
                    {
                        Navigate(session, world, player, target, selectedKey,
                            ref targetKey, ref targetDistance, ref targetStallTicks,
                            ref navigationCorrections);
                        session.SetInteractHeld(IsInteractionTargetInRange(
                            world, player, target, selectedKey));
                    }
                    else
                    {
                        var angle = (world.Tick % (20 * SimulationClock.TickRate)) *
                                    (Math.PI * 2d / (20d * SimulationClock.TickRate));
                        session.SetMoveDirection(new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)));
                        session.SetInteractHeld(false);
                    }

                    if (world.Tick % SimulationClock.TickRate == 0)
                        QueueCombatDriverDamage(world, player);
                    var advanced = session.Advance(SimulationClock.TickDurationSeconds);
                    if (advanced < 0 || advanced > 1)
                        throw new InvalidOperationException("Vertical-slice driver advanced an invalid tick count.");
                    ProgressEvents(world);
                    ResolveChoices(session, route, ref selections, ref rewards);
                    CaptureBossState(world, ref phaseMaskZhezhi, ref phaseMaskTingfeng, ref tingfengRuleMask);

                    peakEnemies = Math.Max(peakEnemies, world.Enemies.Count);
                    maximumViews = Math.Max(maximumViews, world.RenderSnapshot.Count);
                    positionsWalkable &= AllPositionsWalkable(world);
                    if (world.Actors.TryRead(player, out var playerState))
                    {
                        if (hasPreviousPosition)
                            movementDistance += Vector2.Distance(previousPlayerPosition, playerState.Position);
                        previousPlayerPosition = playerState.Position;
                        hasPreviousPosition = true;
                    }
                }

                if (!session.HasEnded)
                    throw new InvalidOperationException("The real run did not reach its victory result before the safety limit.");
                var result = session.Result;
                var statistics = result.Statistics;
                var expectedRuleMask = route == 0 ? 1 : route == 1 ? 6 : 7;
                var combined = Combine(result, route);
                var summary = new QinglanG28SliceSummary
                {
                    seed = "0x" + seed.ToString("X16", CultureInfo.InvariantCulture),
                    route = route,
                    expectedTingfengRuleMask = expectedRuleMask,
                    tingfengRuleMask = tingfengRuleMask,
                    completedTicks = result.CompletedTicks,
                    durationSeconds = result.DurationSeconds,
                    victory = result.IsVictory,
                    level = result.Level,
                    skillCount = result.SkillCount,
                    passiveCount = result.PassiveCount,
                    relicCount = result.RelicCount,
                    evolutionCount = result.EvolutionCount,
                    enemyDefeats = statistics.EnemyDefeats,
                    eliteDefeats = statistics.EliteDefeats,
                    bossDefeats = statistics.BossDefeats,
                    pickupsCollected = statistics.PickupsCollected,
                    offersSelected = statistics.OffersSelected,
                    rewardChoicesSelected = rewards,
                    completedObjectives = result.Exploration.CompletedObjectiveIds.Count,
                    completedEvents = result.Exploration.CompletedEventIds.Count,
                    discoveredLandmarks = result.Exploration.DiscoveredLandmarkIds.Count,
                    claimedLandmarks = result.Exploration.ClaimedLandmarkIds.Count,
                    zhezhiPhaseMask = phaseMaskZhezhi,
                    tingfengPhaseMask = phaseMaskTingfeng,
                    peakEnemies = peakEnemies,
                    peakSnapshotEntities = maximumViews,
                    movementDistance = movementDistance,
                    navigationCorrections = navigationCorrections,
                    positionsWalkable = positionsWalkable,
                    invalidHandleAccesses = world.Diagnostics.InvalidHandleAccesses,
                    spawnChecksum = result.SpawnChecksum.ToString("x16", CultureInfo.InvariantCulture),
                    objectiveChecksum = result.ObjectiveChecksum.ToString("x16", CultureInfo.InvariantCulture),
                    bossChecksum = result.BossChecksum.ToString("x16", CultureInfo.InvariantCulture),
                    decisionChecksum = statistics.DecisionChecksum.ToString("x16", CultureInfo.InvariantCulture),
                    combinedChecksum = combined.ToString("x16", CultureInfo.InvariantCulture),
                    activeEntitiesBeforeDispose = handle.ActiveEntityCount
                };
                return summary;
            }
            finally
            {
                handle.Dispose();
                if (!handle.IsDisposed || handle.ActiveEntityCount != 0)
                    throw new InvalidOperationException("Vertical-slice run owner did not release all entities.");
            }
        }

        public static QinglanG28SpawnFairnessSummary RunSpawnFairness(
            ContentRegistry content,
            ulong seed)
        {
            if (!content.TryGet(Id(QinglanDemoRunFactory.MapId), out RuntimeMapDefinition mapDefinition))
                throw new InvalidOperationException("Qinglan map is missing.");
            if (!content.TryGet(mapDefinition.EncounterScheduleId, out RuntimeEncounterSchedule schedule))
                throw new InvalidOperationException("Qinglan encounter is missing.");
            var map = MapRuntimeFactory.Create(mapDefinition, seed);
            var enemies = new EnemyRuntime(EnemyRuntimeCatalog.Build(content).Value,
                DifficultySnapshot.Default, schedule.MaximumConcurrentEnemies + 32);
            var scheduler = new EncounterScheduler(schedule, map, DifficultySnapshot.Default, seed);
            var world = new SimulationWorld(
                seed,
                schedule.MaximumConcurrentEnemies + 32,
                2f,
                new SimulationPipeline(new SpawnSchedulerSystem()),
                null,
                null,
                null,
                enemies,
                map,
                scheduler);
            var player = world.CreateActor(
                SimulationEntityState.Create(Vector2.Zero, Vector2.Zero),
                ActorCombatInitialization.CreateDefault(1_000_000f, 0f));
            world.SetPlayer(player);
            var runner = new FixedTickRunner(world);
            var normalRequests = 0;
            var bossRequests = 0;
            var minimumDistance = float.MaxValue;
            var maximumDistance = 0f;
            var walkable = true;
            var protectedDistance = true;

            for (var tick = 0; tick < MinimumRunTicks; tick++)
            {
                if (runner.Advance(SimulationClock.TickDurationSeconds) != 1)
                    throw new InvalidOperationException("Spawn-fairness probe must advance exactly one tick.");
                for (var index = 0; index < enemies.PendingSpawns.Count; index++)
                {
                    var request = enemies.PendingSpawns.GetAt(index);
                    walkable &= map.IsWalkable(request.Position);
                    if (request.Boss)
                    {
                        bossRequests++;
                        continue;
                    }
                    if (request.SplitGeneration != 0) continue;
                    normalRequests++;
                    var distance = request.Position.Length();
                    minimumDistance = Math.Min(minimumDistance, distance);
                    maximumDistance = Math.Max(maximumDistance, distance);
                    protectedDistance &= distance + 0.001f >= schedule.MinimumSpawnDistance;
                }
                enemies.PendingSpawns.Clear();
            }

            var passed = normalRequests > 0 && bossRequests == 2 && walkable && protectedDistance &&
                         scheduler.BossRequestCount == 2 && scheduler.AccumulatedBudget == 0f;
            return new QinglanG28SpawnFairnessSummary
            {
                passed = passed,
                tickCount = world.Tick,
                normalRequests = normalRequests,
                bossRequests = bossRequests,
                minimumDistance = minimumDistance,
                maximumDistance = maximumDistance,
                allPositionsWalkable = walkable,
                spawnProtectionRespected = protectedDistance,
                schedulerStoppedAtDuration = scheduler.AccumulatedBudget == 0f
            };
        }

        private static GameApplication CreateApplication(BakedContentCatalog[] catalogs)
        {
            return QinglanDemoRunFactory.CreateInitializedApplicationForDiagnostics(catalogs, GameVersion);
        }

        private static void ApplyDriverSafety(SimulationWorld world, EntityHandle player)
        {
            if (!world.Actors.TryAddModifier(player,
                    new Modifier(DriverSourceId, BuiltInStatIds.Health, ModifierOperation.AddFlat,
                        1_000_000f, 10_000, default, float.PositiveInfinity), out _))
                throw new InvalidOperationException("Unable to install driver health guard.");
            if (!world.Actors.TryAddModifier(player,
                    new Modifier(DriverSourceId, BuiltInStatIds.PickupRange, ModifierOperation.AddFlat,
                        256f, 10_000, default, float.PositiveInfinity), out _))
                throw new InvalidOperationException("Unable to install driver pickup guard.");
            world.Actors.TryApplyHealing(player, 1_000_000f);
        }

        private static bool TryChooseTarget(
            ContentRegistry content,
            SimulationWorld world,
            int route,
            out Vector2 target,
            out string key)
        {
            var objectives = world.Qinglan.MapObjectives;
            for (var index = 0; index < objectives.ObjectiveCount; index++)
            {
                var snapshot = objectives.GetObjectiveAt(index);
                if (!WantsObjective(route, snapshot.Id) || snapshot.State == ObjectiveState.Completed) continue;
                var anchorId = snapshot.ActiveAnchorId;
                if (!anchorId.IsValid && content.TryGet(snapshot.Id, out RuntimeMapObjectiveDefinition definition) &&
                    definition.AnchorIds.Count > 0)
                    anchorId = definition.AnchorIds[0];
                if (anchorId.IsValid && world.Map.TryGetAnchor(anchorId, out target))
                {
                    key = snapshot.Id.Value;
                    return true;
                }
            }
            for (var index = 0; index < objectives.LandmarkCount; index++)
            {
                var snapshot = objectives.GetLandmarkAt(index);
                if (snapshot.State == LandmarkState.Claimed ||
                    !world.Map.TryGetAnchor(snapshot.AnchorId, out target)) continue;
                key = snapshot.Id.Value;
                return true;
            }
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (!world.Enemies.TryGetSnapshot(handle, out var enemy) || !enemy.Boss) continue;
                target = world.Actors.GetStateAt(index).Position;
                key = "boss:" + handle.Index.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            target = default;
            key = string.Empty;
            return false;
        }

        private static void Navigate(
            RunSession session,
            SimulationWorld world,
            EntityHandle player,
            Vector2 target,
            string selectedKey,
            ref string targetKey,
            ref float bestDistance,
            ref int stallTicks,
            ref int corrections)
        {
            if (!world.Actors.TryRead(player, out var state)) return;
            var offset = target - state.Position;
            var distance = offset.Length();
            if (!string.Equals(targetKey, selectedKey, StringComparison.Ordinal))
            {
                targetKey = selectedKey;
                bestDistance = distance;
                stallTicks = 0;
            }
            else if (distance + 0.05f < bestDistance)
            {
                bestDistance = distance;
                stallTicks = 0;
            }
            else
            {
                stallTicks++;
            }

            if (stallTicks >= 180 && distance > 2f)
            {
                state.Position = world.Map.ResolveMovement(state.Position, target, 0.35f);
                state.Velocity = Vector2.Zero;
                world.Actors.TryWrite(player, state);
                corrections++;
                stallTicks = 0;
                bestDistance = Vector2.Distance(state.Position, target);
                offset = target - state.Position;
                distance = offset.Length();
            }
            session.SetMoveDirection(distance <= 0.35f ? Vector2.Zero : Vector2.Normalize(offset));
        }

        private static bool IsInteractionTargetInRange(
            SimulationWorld world,
            EntityHandle player,
            Vector2 target,
            string key)
        {
            if (string.IsNullOrEmpty(key) || key.StartsWith("boss:", StringComparison.Ordinal) ||
                !world.Actors.TryRead(player, out var state)) return false;
            return Vector2.DistanceSquared(state.Position, target) <= 2.25f * 2.25f;
        }

        private static void ProgressEvents(SimulationWorld world)
        {
            var map = world.Qinglan.MapObjectives;
            var delta = (float)SimulationClock.TickDurationSeconds / 4f;
            for (var index = 0; index < map.EventCount; index++)
            {
                var snapshot = map.GetEventAt(index);
                if (snapshot.State == ObjectiveState.Defending)
                    map.ReportEventProgress(snapshot.Id, delta);
            }
        }

        private static void ResolveChoices(
            RunSession session,
            int route,
            ref int selections,
            ref int rewards)
        {
            if (session.StateMachine.CurrentState == GameState.LevelUpChoice)
            {
                var offers = session.CurrentOffers;
                if (offers == null || offers.Count == 0 ||
                    !session.SelectAt((route + selections) % offers.Count))
                    throw new InvalidOperationException("The automatic player could not resolve an upgrade choice.");
                selections++;
            }
            if (session.StateMachine.CurrentState == GameState.RewardChoice)
            {
                var choice = session.CurrentRewardChoice;
                if (choice == null || choice.CandidateIds.Count == 0 ||
                    !session.SelectRewardAt((route + rewards) % choice.CandidateIds.Count))
                    throw new InvalidOperationException("The automatic player could not resolve a reward choice.");
                rewards++;
            }
        }

        private static void QueueCombatDriverDamage(SimulationWorld world, EntityHandle player)
        {
            for (var dense = 0; dense < world.Actors.Count; dense++)
            {
                var handle = world.Actors.GetHandleAt(dense);
                if (!world.Enemies.TryGetSnapshot(handle, out var enemy)) continue;
                var damage = 1_000_000_000f;
                if (enemy.Boss && world.Actors.TryReadHealth(handle, out var health))
                {
                    var fraction = health.Maximum <= 0f ? 0f : health.Current / health.Maximum;
                    damage = fraction <= 0.12f ? health.Maximum : health.Maximum * 0.15f;
                }
                world.QueueDamage(new DamagePacket(
                    new SpatialEntity(EntityKind.Actor, player),
                    new SpatialEntity(EntityKind.Actor, handle),
                    DriverSourceId,
                    DamageType.True,
                    DamageTags.Direct,
                    damage,
                    false,
                    0f,
                    Vector2.Zero,
                    world.Actors.GetStateAt(dense).Position,
                    0));
            }
        }

        private static void CaptureBossState(
            SimulationWorld world,
            ref int zhezhiPhases,
            ref int tingfengPhases,
            ref int tingfengRuleMask)
        {
            for (var index = 0; index < world.Actors.Count; index++)
            {
                var handle = world.Actors.GetHandleAt(index);
                if (!world.Qinglan.Bosses.TryGet(handle, out var boss)) continue;
                if (boss.Phase < boss.PhaseCount)
                {
                    if (boss.BossId == ZhezhiBossId) zhezhiPhases |= 1 << boss.Phase;
                    if (boss.BossId == TingfengBossId) tingfengPhases |= 1 << boss.Phase;
                }
                if (boss.BossId == TingfengBossId &&
                    world.Qinglan.Bosses.TryGetModifierSnapshot(handle, out var modifiers))
                    tingfengRuleMask = modifiers.ActiveRuleMask;
            }
        }

        private static bool AllPositionsWalkable(SimulationWorld world)
        {
            if (world.Map == null) return true;
            for (var index = 0; index < world.Actors.Count; index++)
                if (!world.Map.IsWalkable(world.Actors.GetStateAt(index).Position)) return false;
            return true;
        }

        private static bool WantsObjective(int route, ContentId id)
        {
            if (route == 2) return id == GuideObjectiveId || id == ListenObjectiveId || id == StopObjectiveId;
            if (route == 0) return id == GuideObjectiveId;
            return id == ListenObjectiveId || id == StopObjectiveId;
        }

        private static bool Valid(QinglanG28SliceSummary value)
        {
            var expectedObjectives = value.route == 0 ? 1 : value.route == 1 ? 2 : 3;
            return value.victory && value.completedTicks >= MinimumRunTicks &&
                   value.bossDefeats == 2 && value.completedObjectives == expectedObjectives &&
                   value.completedEvents == 3 && value.claimedLandmarks == 5 &&
                   value.offersSelected > 0 && value.rewardChoicesSelected >= 2 &&
                   value.relicCount > 0 && value.zhezhiPhaseMask == 7 &&
                   value.tingfengPhaseMask == 7 &&
                   value.tingfengRuleMask == value.expectedTingfengRuleMask &&
                   value.movementDistance > 50d && value.positionsWalkable &&
                   value.invalidHandleAccesses == 0 && value.activeEntitiesBeforeDispose > 0;
        }

        private static bool Equivalent(QinglanG28SliceSummary left, QinglanG28SliceSummary right)
        {
            return left.completedTicks == right.completedTicks && left.level == right.level &&
                   left.skillCount == right.skillCount && left.passiveCount == right.passiveCount &&
                   left.relicCount == right.relicCount && left.evolutionCount == right.evolutionCount &&
                   left.enemyDefeats == right.enemyDefeats && left.eliteDefeats == right.eliteDefeats &&
                   left.bossDefeats == right.bossDefeats && left.pickupsCollected == right.pickupsCollected &&
                   left.offersSelected == right.offersSelected &&
                   left.completedObjectives == right.completedObjectives &&
                   left.completedEvents == right.completedEvents &&
                   left.claimedLandmarks == right.claimedLandmarks &&
                   left.tingfengRuleMask == right.tingfengRuleMask &&
                   left.spawnChecksum == right.spawnChecksum &&
                   left.objectiveChecksum == right.objectiveChecksum &&
                   left.bossChecksum == right.bossChecksum &&
                   left.decisionChecksum == right.decisionChecksum &&
                   left.combinedChecksum == right.combinedChecksum;
        }

        private static ulong Combine(in RunResult result, int route)
        {
            unchecked
            {
                var hash = result.SpawnChecksum;
                hash = (hash ^ result.ObjectiveChecksum) * 1099511628211UL;
                hash = (hash ^ result.BossChecksum) * 1099511628211UL;
                hash = (hash ^ result.Statistics.DecisionChecksum) * 1099511628211UL;
                hash = (hash ^ (ulong)result.CompletedTicks) * 1099511628211UL;
                hash = (hash ^ (uint)route) * 1099511628211UL;
                return hash;
            }
        }

        private static string ResolveOutputPath()
        {
            var configured = Environment.GetEnvironmentVariable("QINGLAN_G28_OUTPUT");
            if (string.IsNullOrWhiteSpace(configured))
                configured = "TestResults/QinglanDemo/G2.8/vertical-slice.json";
            return Path.GetFullPath(configured);
        }

        private static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }
    }

    [Serializable]
    public sealed class QinglanG28VerticalSliceReport
    {
        public int schemaVersion;
        public string status;
        public string failureReason;
        public string generatedAtUtc;
        public string unityVersion;
        public int minimumRequiredTicks;
        public bool deterministicReplay;
        public bool threeBuildRoutesDistinct;
        public QinglanG28SpawnFairnessSummary spawnFairness;
        public QinglanG28SliceSummary primary;
        public QinglanG28SliceSummary repeated;
        public QinglanG28SliceSummary mobilityRoute;
        public QinglanG28SliceSummary fieldRoute;
    }

    [Serializable]
    public sealed class QinglanG28SliceSummary
    {
        public string seed;
        public int route;
        public int expectedTingfengRuleMask;
        public int tingfengRuleMask;
        public long completedTicks;
        public double durationSeconds;
        public bool victory;
        public int level;
        public int skillCount;
        public int passiveCount;
        public int relicCount;
        public int evolutionCount;
        public long enemyDefeats;
        public long eliteDefeats;
        public long bossDefeats;
        public long pickupsCollected;
        public int offersSelected;
        public int rewardChoicesSelected;
        public int completedObjectives;
        public int completedEvents;
        public int discoveredLandmarks;
        public int claimedLandmarks;
        public int zhezhiPhaseMask;
        public int tingfengPhaseMask;
        public int peakEnemies;
        public int peakSnapshotEntities;
        public double movementDistance;
        public int navigationCorrections;
        public bool positionsWalkable;
        public long invalidHandleAccesses;
        public string spawnChecksum;
        public string objectiveChecksum;
        public string bossChecksum;
        public string decisionChecksum;
        public string combinedChecksum;
        public int activeEntitiesBeforeDispose;
    }

    [Serializable]
    public sealed class QinglanG28SpawnFairnessSummary
    {
        public bool passed;
        public long tickCount;
        public int normalRequests;
        public int bossRequests;
        public float minimumDistance;
        public float maximumDistance;
        public bool allPositionsWalkable;
        public bool spawnProtectionRespected;
        public bool schedulerStoppedAtDuration;
    }
}
