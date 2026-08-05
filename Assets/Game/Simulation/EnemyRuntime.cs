using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Compiled enemy definition with its attack skill resolved to a load-local index.</summary>
    public sealed class CompiledEnemyDefinition
    {
        internal CompiledEnemyDefinition(
            RuntimeContentIndex index,
            RuntimeEnemyDefinition source,
            RuntimeContentIndex attackSkillIndex)
        {
            Index = index;
            Source = source;
            AttackSkillIndex = attackSkillIndex;
        }

        public RuntimeContentIndex Index { get; }
        public RuntimeEnemyDefinition Source { get; }
        public RuntimeContentIndex AttackSkillIndex { get; }
    }

    internal sealed class CompiledEliteAffixDefinition
    {
        public CompiledEliteAffixDefinition(
            RuntimeEliteAffixDefinition source,
            RuntimeBuildModifier[] modifiers,
            RuntimeContentIndex skillIndex,
            RuntimeRewardDefinition deathReward)
        {
            Source = source;
            Modifiers = modifiers ?? Array.Empty<RuntimeBuildModifier>();
            SkillIndex = skillIndex;
            DeathReward = deathReward;
        }

        public RuntimeEliteAffixDefinition Source { get; }
        public RuntimeBuildModifier[] Modifiers { get; }
        public RuntimeContentIndex SkillIndex { get; }
        public RuntimeRewardDefinition DeathReward { get; }
    }

    /// <summary>Load-local compiled M5 enemy catalog.</summary>
    public sealed class EnemyRuntimeCatalog
    {
        private readonly CompiledEnemyDefinition[] byIndex;
        private readonly Dictionary<ContentId, CompiledEnemyDefinition> byId;
        private readonly Dictionary<ContentId, CompiledEliteAffixDefinition> affixesById;

        private EnemyRuntimeCatalog(
            CompiledEnemyDefinition[] definitions,
            Dictionary<ContentId, CompiledEnemyDefinition> definitionsById,
            Dictionary<ContentId, CompiledEliteAffixDefinition> compiledAffixes)
        {
            byIndex = definitions;
            byId = definitionsById;
            affixesById = compiledAffixes;
        }

        public int Count => byId.Count;

        public static Result<EnemyRuntimeCatalog> Build(ContentRegistry content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var definitions = new CompiledEnemyDefinition[content.Count];
            var definitionsById = new Dictionary<ContentId, CompiledEnemyDefinition>();
            for (var index = 0; index < content.Count; index++)
            {
                var entry = content.Get(new RuntimeContentIndex(index));
                if (!entry.IsSuccess) return Result<EnemyRuntimeCatalog>.Failure(entry.Error);
                if (!(entry.Value.Definition is RuntimeEnemyDefinition enemy) || !enemy.HasM5Data)
                    continue;
                if (!content.TryGet(enemy.AttackSkillId, out var skillEntry) ||
                    !(skillEntry.Definition is RuntimeSkillDefinition skill) || !skill.IsExecutable)
                {
                    return Result<EnemyRuntimeCatalog>.Failure(
                        new Error(
                            ErrorCode.InvalidAuthoringData,
                            "Enemy attack SkillId was not resolved to an executable skill.",
                            enemy.Id,
                            entry.Value.SourcePackId,
                            enemy.SourceAssetPath));
                }

                var compiled = new CompiledEnemyDefinition(
                    entry.Value.Index,
                    enemy,
                    skillEntry.Index);
                definitions[index] = compiled;
                definitionsById.Add(enemy.Id, compiled);
            }

            var compiledAffixes = new Dictionary<ContentId, CompiledEliteAffixDefinition>();
            for (var index = 0; index < content.Count; index++)
            {
                var entry = content.Get(new RuntimeContentIndex(index));
                if (!entry.IsSuccess) return Result<EnemyRuntimeCatalog>.Failure(entry.Error);
                if (!(entry.Value.Definition is RuntimeEliteAffixDefinition affix)) continue;

                var modifierResult = CompileModifiers(content, affix, entry.Value.SourcePackId);
                if (!modifierResult.IsSuccess)
                    return Result<EnemyRuntimeCatalog>.Failure(modifierResult.Error);

                var skillIndex = default(RuntimeContentIndex);
                if (affix.SkillId.IsValid)
                {
                    if (!content.TryGet(affix.SkillId, out var skillEntry) ||
                        !(skillEntry.Definition is RuntimeSkillDefinition skill) ||
                        !skill.IsExecutable)
                    {
                        return InvalidAffix(
                            affix,
                            entry.Value.SourcePackId,
                            "Elite affix SkillId was not resolved to an executable skill.");
                    }
                    skillIndex = skillEntry.Index;
                }

                RuntimeRewardDefinition deathReward = null;
                if (affix.DeathRewardId.IsValid &&
                    (!content.TryGet(affix.DeathRewardId, out var rewardEntry) ||
                     (deathReward = rewardEntry.Definition as RuntimeRewardDefinition) == null))
                {
                    return InvalidAffix(
                        affix,
                        entry.Value.SourcePackId,
                        "Elite affix DeathRewardId was not resolved to a Reward.");
                }

                compiledAffixes.Add(
                    affix.Id,
                    new CompiledEliteAffixDefinition(
                        affix,
                        modifierResult.Value,
                        skillIndex,
                        deathReward));
            }

            return Result<EnemyRuntimeCatalog>.Success(
                new EnemyRuntimeCatalog(definitions, definitionsById, compiledAffixes));
        }

        private static Result<RuntimeBuildModifier[]> CompileModifiers(
            ContentRegistry content,
            RuntimeEliteAffixDefinition affix,
            ContentId packId)
        {
            if (!affix.ModifierOutputId.IsValid)
                return Result<RuntimeBuildModifier[]>.Success(Array.Empty<RuntimeBuildModifier>());
            if (!content.TryGet(affix.ModifierOutputId, out var outputEntry))
                return Result<RuntimeBuildModifier[]>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Elite affix modifier output was not resolved.",
                        affix.Id,
                        packId,
                        affix.SourceAssetPath));

            var output = new List<RuntimeBuildModifier>();
            if (outputEntry.Definition is RuntimeTraitDefinition trait)
            {
                for (var index = 0; index < trait.Modifiers.Count; index++)
                    output.Add(trait.Modifiers[index]);
            }
            else if (outputEntry.Definition is RuntimePassiveDefinition passive)
            {
                for (var index = 0; index < passive.LevelModifiers.Count; index++)
                    output.Add(passive.LevelModifiers[index].Modifier);
            }
            else if (outputEntry.Definition is RuntimeSynergyDefinition synergy)
            {
                for (var index = 0; index < synergy.Outputs.Count; index++)
                    if (synergy.Outputs[index].Type == SynergyOutputType.AddModifier)
                        output.Add(synergy.Outputs[index].Modifier);
            }
            else
            {
                return Result<RuntimeBuildModifier[]>.Failure(
                    new Error(
                        ErrorCode.InvalidAuthoringData,
                        "Elite affix modifier output has an unsupported runtime type.",
                        affix.Id,
                        packId,
                        affix.SourceAssetPath));
            }

            return Result<RuntimeBuildModifier[]>.Success(output.ToArray());
        }

        private static Result<EnemyRuntimeCatalog> InvalidAffix(
            RuntimeEliteAffixDefinition affix,
            ContentId packId,
            string message) =>
            Result<EnemyRuntimeCatalog>.Failure(
                new Error(
                    ErrorCode.InvalidAuthoringData,
                    message,
                    affix.Id,
                    packId,
                    affix.SourceAssetPath));

        public bool TryGet(RuntimeContentIndex index, out CompiledEnemyDefinition definition)
        {
            if (!index.IsValid || index.Value >= byIndex.Length)
            {
                definition = null;
                return false;
            }

            definition = byIndex[index.Value];
            return definition != null;
        }

        public bool TryGet(ContentId id, out CompiledEnemyDefinition definition) =>
            byId.TryGetValue(id, out definition);

        internal bool TryGetAffix(ContentId id, out CompiledEliteAffixDefinition definition) =>
            affixesById.TryGetValue(id, out definition);

        internal static EnemyRuntimeCatalog Empty() =>
            new EnemyRuntimeCatalog(
                Array.Empty<CompiledEnemyDefinition>(),
                new Dictionary<ContentId, CompiledEnemyDefinition>(),
                new Dictionary<ContentId, CompiledEliteAffixDefinition>());
    }

    public enum EnemyBehaviorState : byte
    {
        Idle = 0,
        Pursuing = 1,
        HoldingRange = 2,
        ChargeWindup = 3,
        Charging = 4,
        Recovering = 5,
        RangedAttack = 6
    }

    /// <summary>Read-only per-enemy diagnostic state exposed to tests and view adapters.</summary>
    public readonly struct EnemyInstanceSnapshot
    {
        internal EnemyInstanceSnapshot(
            ContentId enemyId,
            EnemyBehaviorState behaviorState,
            bool elite,
            bool boss,
            float experienceReward,
            float lootReward)
        {
            EnemyId = enemyId;
            BehaviorState = behaviorState;
            Elite = elite;
            Boss = boss;
            ExperienceReward = experienceReward;
            LootReward = lootReward;
        }

        public ContentId EnemyId { get; }
        public EnemyBehaviorState BehaviorState { get; }
        public bool Elite { get; }
        public bool Boss { get; }
        public float ExperienceReward { get; }
        public float LootReward { get; }
    }

    internal struct EnemyInstance
    {
        public ushort Generation;
        public CompiledEnemyDefinition Definition;
        public EnemyBehaviorState State;
        public float StateTimer;
        public float DecisionTimer;
        public float AttackTimer;
        public Vector2 ChargeDirection;
        public EliteAffixSelection Affixes;
        public int SplitGeneration;
        public float RewardScale;
        public bool Elite;
        public bool Boss;
    }

    /// <summary>
    /// Centralized dense-sidecar enemy runtime. One system advances all enemies; no actor owns Update.
    /// </summary>
    public sealed class EnemyRuntime
    {
        private readonly DifficultySnapshot difficulty;
        private readonly SpatialQueryBuffer neighbors;
        private EnemyInstance[] instances;
        private long splitSpawnSequence = 1L << 61;

        public EnemyRuntime(
            EnemyRuntimeCatalog catalog,
            in DifficultySnapshot difficultySnapshot,
            int initialCapacity = 64)
        {
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            difficulty = difficultySnapshot;
            instances = new EnemyInstance[initialCapacity];
            neighbors = new SpatialQueryBuffer(initialCapacity);
            PendingSpawns = new SpawnRequestBuffer(initialCapacity);
        }

        public EnemyRuntimeCatalog Catalog { get; }
        public SpawnRequestBuffer PendingSpawns { get; }
        public int Count { get; private set; }
        public EntityHandle Player { get; private set; }
        public long SpawnedCount { get; private set; }
        public int BossSpawnedCount { get; private set; }
        public ulong SpawnChecksum { get; private set; } = 1469598103934665603UL;

        public void SetPlayer(EntityHandle player)
        {
            Player = player;
        }

        public bool IsEnemy(EntityHandle handle)
        {
            return handle.IsValid && handle.Index < instances.Length &&
                   instances[handle.Index].Definition != null &&
                   instances[handle.Index].Generation == handle.Generation;
        }

        public bool TryGetSnapshot(EntityHandle handle, out EnemyInstanceSnapshot snapshot)
        {
            if (!TryGetInstance(handle, out var instance))
            {
                snapshot = default;
                return false;
            }

            var eliteMultiplier = instance.Elite ? 1.5f : 1f;
            snapshot = new EnemyInstanceSnapshot(
                instance.Definition.Source.Id,
                instance.State,
                instance.Elite,
                instance.Boss,
                instance.Definition.Source.ExperienceReward * difficulty.RewardMultiplier * eliteMultiplier * instance.RewardScale,
                instance.Definition.Source.LootReward * difficulty.RewardMultiplier * eliteMultiplier * instance.RewardScale);
            return true;
        }

        internal EliteAffixSelection ComposeAffixes(
            CompiledEnemyDefinition enemy,
            IReadOnlyList<ContentId> affixPoolIds,
            bool elite,
            bool boss,
            ref RandomStream random)
        {
            if (!elite || boss || enemy == null || affixPoolIds == null || affixPoolIds.Count == 0)
                return default;

            CompiledEliteAffixDefinition first = null;
            CompiledEliteAffixDefinition second = null;
            var start = affixPoolIds.Count > 1 ? random.NextInt(affixPoolIds.Count) : 0;
            for (var offset = 0; offset < affixPoolIds.Count && second == null; offset++)
            {
                var poolIndex = (start + offset) % affixPoolIds.Count;
                if (!Catalog.TryGetAffix(affixPoolIds[poolIndex], out var candidate) ||
                    !IsAffixCompatible(enemy.Source, candidate, first))
                {
                    continue;
                }

                if (first == null) first = candidate;
                else if (candidate != first) second = candidate;
            }

            return new EliteAffixSelection(first, second);
        }

        internal bool TryGetAffixId(EntityHandle handle, int index, out ContentId id)
        {
            if (!TryGetInstance(handle, out var instance) ||
                index < 0 || index >= instance.Affixes.Count)
            {
                id = default;
                return false;
            }

            id = instance.Affixes.GetAt(index).Source.Id;
            return true;
        }

        internal int GetAffixCount(EntityHandle handle) =>
            TryGetInstance(handle, out var instance) ? instance.Affixes.Count : 0;

        internal int GetSplitGeneration(EntityHandle handle) =>
            TryGetInstance(handle, out var instance) ? instance.SplitGeneration : -1;

        internal static EnemyRuntime CreateEmpty(int capacity = 16) =>
            new EnemyRuntime(EnemyRuntimeCatalog.Empty(), DifficultySnapshot.Default, capacity);

        internal void TickDecisions(SimulationWorld world)
        {
            if (!Player.IsValid || !world.Actors.TryRead(Player, out var playerState))
                return;
            for (var dense = 0; dense < world.Actors.Count; dense++)
            {
                var handle = world.Actors.GetHandleAt(dense);
                if (!TryGetInstance(handle, out var instance)) continue;
                var state = world.Actors.GetStateAt(dense);
                var offset = playerState.Position - state.Position;
                var distanceSquared = offset.LengthSquared();
                var direction = NormalizeOrFallback(offset, Vector2.UnitX);
                instance.DecisionTimer -= world.DeltaTimeSeconds;
                instance.AttackTimer = Math.Max(0f, instance.AttackTimer - world.DeltaTimeSeconds);
                var velocity = DecideVelocity(
                    world,
                    handle,
                    state.Position,
                    direction,
                    distanceSquared,
                    ref instance);
                velocity = ApplySeparation(world, handle, state.Position, velocity, instance);
                velocity = ApplyObstacleAvoidance(world, state.Position, velocity, instance);
                if (!Finite(velocity)) velocity = Vector2.Zero;
                state.Velocity = velocity;
                world.Actors.SetStateAt(dense, state);
                instances[handle.Index] = instance;
            }
        }

        internal void ApplyPendingSpawns(SimulationWorld world)
        {
            var count = PendingSpawns.Count;
            for (var index = 0; index < count; index++)
            {
                var request = PendingSpawns.GetAt(index);
                if (!Catalog.TryGet(request.EnemyIndex, out var definition))
                    throw new InvalidOperationException("Spawn request references an unknown enemy index.");
                var eliteMultiplier = request.Elite ? 1.5f : 1f;
                var affixRewardMultiplier = 1f;
                for (var affixIndex = 0; affixIndex < request.Affixes.Count; affixIndex++)
                    affixRewardMultiplier *= request.Affixes.GetAt(affixIndex).Source.RewardMultiplier;
                var health = definition.Source.BaseMaxHealth * difficulty.HealthMultiplier * eliteMultiplier * request.CombatScale;
                var moveSpeed = definition.Source.BaseMoveSpeed * difficulty.SpeedMultiplier;
                var stats = StatBaseValues.CreateDefault(health, moveSpeed);
                stats.Damage = definition.Source.BaseDamage * difficulty.DamageMultiplier * eliteMultiplier * request.CombatScale;
                var combat = new ActorCombatInitialization(stats, health, 0f, 0f, default);
                var state = SimulationEntityState.Create(request.Position, Vector2.Zero);
                var handle = world.CreateActor(state, combat);
                EnsureCapacity(handle.Index + 1);
                instances[handle.Index] = new EnemyInstance
                {
                    Generation = handle.Generation,
                    Definition = definition,
                    State = EnemyBehaviorState.Idle,
                    DecisionTimer = 0f,
                    AttackTimer = 0f,
                    Affixes = request.Affixes,
                    SplitGeneration = request.SplitGeneration,
                    RewardScale = request.RewardScale * affixRewardMultiplier,
                    Elite = request.Elite,
                    Boss = request.Boss
                };
                Count++;
                SpawnedCount++;
                if (request.Boss) BossSpawnedCount++;
                UpdateSpawnChecksum(request);
                var skill = world.Skills.AddInstance(
                    new SpatialEntity(EntityKind.Actor, handle),
                    definition.AttackSkillIndex);
                if (!skill.IsSuccess)
                    throw new InvalidOperationException("Enemy attack skill could not be instantiated: " + skill.Error.Message);
                ApplyAffixOutputs(world, handle, request.Affixes);
                world.EmitEvent(SimulationEventType.Created, EntityKind.Actor, handle, request.Position);
            }

            PendingSpawns.Clear();
        }

        private void UpdateSpawnChecksum(in SpawnRequest request)
        {
            unchecked
            {
                SpawnChecksum ^= (uint)request.EnemyIndex.Value;
                SpawnChecksum *= 1099511628211UL;
                SpawnChecksum ^= (uint)BitConverter.SingleToInt32Bits(request.Position.X);
                SpawnChecksum *= 1099511628211UL;
                SpawnChecksum ^= (uint)BitConverter.SingleToInt32Bits(request.Position.Y);
                SpawnChecksum *= 1099511628211UL;
                SpawnChecksum ^= (ulong)request.Sequence;
                SpawnChecksum *= 1099511628211UL;
                SpawnChecksum ^= request.Boss ? 1UL : 0UL;
                SpawnChecksum *= 1099511628211UL;
                SpawnChecksum ^= (ulong)request.Affixes.Count;
                SpawnChecksum *= 1099511628211UL;
                for (var index = 0; index < request.Affixes.Count; index++)
                {
                    SpawnChecksum ^= (ulong)request.Affixes.GetAt(index).Source.Id.GetHashCode();
                    SpawnChecksum *= 1099511628211UL;
                }
                SpawnChecksum ^= (uint)request.SplitGeneration;
                SpawnChecksum *= 1099511628211UL;
            }
        }

        internal void ProcessDeathOutputs(
            SimulationWorld world,
            EntityHandle handle,
            Vector2 position)
        {
            if (world == null || !TryGetInstance(handle, out var instance))
            {
                return;
            }

            for (var affixIndex = 0; affixIndex < instance.Affixes.Count; affixIndex++)
            {
                var affix = instance.Affixes.GetAt(affixIndex);
                var reward = affix.DeathReward;
                if (reward == null) continue;
                for (var operationIndex = 0; operationIndex < reward.Operations.Count; operationIndex++)
                {
                    var operation = reward.Operations[operationIndex];
                    if (operation.Code != RewardOperationCode.SpawnEnemy ||
                        instance.SplitGeneration >= affix.Source.MaximumGeneration)
                    {
                        continue;
                    }
                    var childDefinition = instance.Definition;
                    if (operation.ReferenceId.IsValid &&
                        !Catalog.TryGet(operation.ReferenceId, out childDefinition))
                    {
                        continue;
                    }

                    var childCount = Math.Min(2, Math.Max(1, operation.IntegerValue));
                    var scale = Math.Min(1f, Math.Max(0.1f, operation.Value));
                    for (var child = 0; child < childCount; child++)
                    {
                        var angle = ((handle.Index * 0.7548777f) + (child * 0.5f)) * 2f * (float)Math.PI;
                        var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) *
                                     Math.Max(0.5f, childDefinition.Source.CollisionRadius * 1.5f);
                        var desired = position + offset;
                        var childPosition = world.Map == null
                            ? desired
                            : world.Map.ResolveMovement(
                                position,
                                desired,
                                childDefinition.Source.CollisionRadius);
                        PendingSpawns.Add(
                            new SpawnRequest(
                                childDefinition.Index,
                                childPosition,
                                false,
                                false,
                                splitSpawnSequence++,
                                default,
                                instance.SplitGeneration + 1,
                                scale,
                                scale));
                    }
                }
            }
        }

        private static bool IsAffixCompatible(
            RuntimeEnemyDefinition enemy,
            CompiledEliteAffixDefinition candidate,
            CompiledEliteAffixDefinition selected)
        {
            for (var index = 0; index < candidate.Source.RequiredTags.Count; index++)
                if (!HasTag(enemy.Tags, candidate.Source.RequiredTags[index]) &&
                    (selected == null || !HasTag(selected.Source.Tags, candidate.Source.RequiredTags[index])))
                    return false;
            for (var index = 0; index < candidate.Source.ExcludedTags.Count; index++)
                if (HasTag(enemy.Tags, candidate.Source.ExcludedTags[index]) ||
                    (selected != null && HasTag(selected.Source.Tags, candidate.Source.ExcludedTags[index])))
                    return false;
            if (selected == null) return true;
            for (var index = 0; index < selected.Source.ExcludedTags.Count; index++)
                if (HasTag(candidate.Source.Tags, selected.Source.ExcludedTags[index])) return false;
            return true;
        }

        private static bool HasTag(IReadOnlyList<ContentTag> tags, ContentTag expected)
        {
            for (var index = 0; index < tags.Count; index++)
                if (tags[index] == expected) return true;
            return false;
        }

        private static void ApplyAffixOutputs(
            SimulationWorld world,
            EntityHandle handle,
            in EliteAffixSelection affixes)
        {
            for (var affixIndex = 0; affixIndex < affixes.Count; affixIndex++)
            {
                var affix = affixes.GetAt(affixIndex);
                for (var modifierIndex = 0; modifierIndex < affix.Modifiers.Length; modifierIndex++)
                {
                    var source = affix.Modifiers[modifierIndex];
                    var modifier = new Modifier(
                        affix.Source.Id,
                        source.StatId,
                        source.Operation,
                        source.Value,
                        source.Priority,
                        source.StackingGroup,
                        float.PositiveInfinity);
                    if (!world.Actors.TryAddModifier(handle, modifier, out _))
                        throw new InvalidOperationException("Elite affix modifier capacity was exhausted.");
                }

                if (affix.SkillIndex.IsValid)
                {
                    var skill = world.Skills.AddInstance(
                        new SpatialEntity(EntityKind.Actor, handle),
                        affix.SkillIndex);
                    if (!skill.IsSuccess)
                        throw new InvalidOperationException("Elite affix skill could not be instantiated: " + skill.Error.Message);
                }
            }

            if (affixes.Count > 0 && world.Actors.TryGetCombat(handle, out var combat))
                combat.ReconcileHealthMaximum();
        }

        internal void OnEntityRemoved(EntityHandle handle)
        {
            if (!IsEnemy(handle)) return;
            instances[handle.Index] = default;
            Count--;
        }

        internal bool IsHostile(SpatialEntity owner, SpatialEntity candidate)
        {
            if (owner.Kind != EntityKind.Actor || candidate.Kind != EntityKind.Actor) return false;
            if (Count == 0) return owner != candidate;
            var ownerEnemy = IsEnemy(owner.Handle);
            var candidateEnemy = IsEnemy(candidate.Handle);
            return ownerEnemy ? !candidateEnemy : candidateEnemy;
        }

        internal Vector2 ResolveMovement(
            IMapRuntime map,
            EntityHandle handle,
            Vector2 currentPosition,
            Vector2 desiredPosition)
        {
            if (map == null) return desiredPosition;
            var radius = TryGetInstance(handle, out var instance)
                ? instance.Definition.Source.CollisionRadius
                : 0f;
            return map.ResolveMovement(currentPosition, desiredPosition, radius);
        }

        private Vector2 DecideVelocity(
            SimulationWorld world,
            EntityHandle handle,
            Vector2 position,
            Vector2 direction,
            float distanceSquared,
            ref EnemyInstance instance)
        {
            var source = instance.Definition.Source;
            var behavior = source.Behavior;
            var moveSpeed = source.BaseMoveSpeed * difficulty.SpeedMultiplier;
            var attackRangeSquared = source.AttackRange * source.AttackRange;
            if (behavior.MovementMode == EnemyMovementMode.Charge)
            {
                if (instance.State == EnemyBehaviorState.ChargeWindup)
                {
                    instance.StateTimer -= world.DeltaTimeSeconds;
                    if (instance.StateTimer <= 0f)
                    {
                        instance.State = EnemyBehaviorState.Charging;
                        instance.StateTimer = Math.Max(world.DeltaTimeSeconds, behavior.ChargeDurationSeconds);
                    }
                    return Vector2.Zero;
                }

                if (instance.State == EnemyBehaviorState.Charging)
                {
                    instance.StateTimer -= world.DeltaTimeSeconds;
                    if (instance.StateTimer <= 0f)
                    {
                        instance.State = EnemyBehaviorState.Recovering;
                        instance.StateTimer = behavior.DecisionIntervalSeconds;
                        return Vector2.Zero;
                    }

                    return instance.ChargeDirection * moveSpeed * behavior.ChargeSpeedMultiplier;
                }

                if (instance.State == EnemyBehaviorState.Recovering)
                {
                    instance.StateTimer -= world.DeltaTimeSeconds;
                    if (instance.StateTimer <= 0f) instance.State = EnemyBehaviorState.Pursuing;
                    return Vector2.Zero;
                }

                if (distanceSquared <= attackRangeSquared && instance.DecisionTimer <= 0f)
                {
                    instance.State = EnemyBehaviorState.ChargeWindup;
                    instance.StateTimer = Math.Max(world.DeltaTimeSeconds, behavior.ChargeWindupSeconds);
                    instance.DecisionTimer = behavior.DecisionIntervalSeconds;
                    instance.ChargeDirection = direction;
                    return Vector2.Zero;
                }

                instance.State = EnemyBehaviorState.Pursuing;
                return direction * moveSpeed;
            }

            var distance = (float)Math.Sqrt(distanceSquared);
            if (behavior.MovementMode == EnemyMovementMode.Chase)
            {
                instance.State = distanceSquared <= attackRangeSquared
                    ? EnemyBehaviorState.Idle
                    : EnemyBehaviorState.Pursuing;
                return distanceSquared <= attackRangeSquared ? Vector2.Zero : direction * moveSpeed;
            }

            var tolerance = Math.Max(0.25f, source.CollisionRadius);
            Vector2 rangedVelocity;
            if (distance > behavior.PreferredDistance + tolerance)
            {
                instance.State = EnemyBehaviorState.Pursuing;
                rangedVelocity = direction * moveSpeed;
            }
            else if (distance < behavior.PreferredDistance - tolerance)
            {
                instance.State = EnemyBehaviorState.HoldingRange;
                rangedVelocity = -direction * moveSpeed;
            }
            else
            {
                instance.State = EnemyBehaviorState.HoldingRange;
                rangedVelocity = Vector2.Zero;
            }

            if (behavior.MovementMode == EnemyMovementMode.Ranged &&
                distanceSquared <= attackRangeSquared && instance.AttackTimer <= 0f)
            {
                instance.State = EnemyBehaviorState.RangedAttack;
                instance.AttackTimer = behavior.AttackCooldownSeconds;
            }

            return rangedVelocity;
        }

        private Vector2 ApplySeparation(
            SimulationWorld world,
            EntityHandle handle,
            Vector2 position,
            Vector2 velocity,
            in EnemyInstance instance)
        {
            var behavior = instance.Definition.Source.Behavior;
            if (behavior.SeparationRadius <= 0f || behavior.SeparationWeight <= 0f)
                return velocity;
            world.SpatialGrid.QueryNearby(
                new SpatialEntity(EntityKind.Actor, handle),
                behavior.SeparationRadius,
                neighbors);
            var separation = Vector2.Zero;
            for (var index = 0; index < neighbors.Count; index++)
            {
                var neighbor = neighbors[index];
                if (neighbor.Entity.Kind != EntityKind.Actor || !IsEnemy(neighbor.Entity.Handle))
                    continue;
                var away = position - neighbor.Position;
                if (away.LengthSquared() <= 0.000001f)
                {
                    away = handle.Index < neighbor.Entity.Handle.Index ? -Vector2.UnitY : Vector2.UnitY;
                }
                separation += NormalizeOrFallback(away, Vector2.UnitY);
            }

            return velocity + separation * behavior.SeparationWeight;
        }

        private static Vector2 ApplyObstacleAvoidance(
            SimulationWorld world,
            Vector2 position,
            Vector2 velocity,
            in EnemyInstance instance)
        {
            if (world.Map == null || instance.Definition.Source.Behavior.ObstacleAvoidanceWeight <= 0f)
                return velocity;
            var desired = position + velocity * world.DeltaTimeSeconds;
            var resolved = world.Map.ResolveMovement(
                position,
                desired,
                instance.Definition.Source.CollisionRadius);
            var correction = (resolved - desired) / world.DeltaTimeSeconds;
            return velocity + correction * instance.Definition.Source.Behavior.ObstacleAvoidanceWeight;
        }

        private bool TryGetInstance(EntityHandle handle, out EnemyInstance instance)
        {
            if (!IsEnemy(handle))
            {
                instance = default;
                return false;
            }

            instance = instances[handle.Index];
            return true;
        }

        private void EnsureCapacity(int required)
        {
            if (required <= instances.Length) return;
            var capacity = instances.Length * 2;
            while (capacity < required) capacity *= 2;
            Array.Resize(ref instances, capacity);
        }

        private static Vector2 NormalizeOrFallback(Vector2 value, Vector2 fallback)
        {
            var lengthSquared = value.LengthSquared();
            return lengthSquared <= 0.000001f ? fallback : value / (float)Math.Sqrt(lengthSquared);
        }

        private static bool Finite(Vector2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }
}
