using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>Condition with any stable StatId resolved once for the current run.</summary>
    public readonly struct CompiledBuildCondition
    {
        internal CompiledBuildCondition(in BuildCondition source, StatIndex statIndex)
        {
            Source = source;
            StatIndex = statIndex;
        }

        public BuildCondition Source { get; }
        public StatIndex StatIndex { get; }
    }

    /// <summary>One compiled, configuration-driven synergy output.</summary>
    public sealed class CompiledSynergyOutput
    {
        internal CompiledSynergyOutput(
            in RuntimeSynergyOutput source,
            RuntimeContentIndex sourceIndex,
            RuntimeContentIndex targetIndex,
            IEffectExecutor effectExecutor,
            in ResolvedEffectOp resolvedEffect)
        {
            Source = source;
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            EffectExecutor = effectExecutor;
            ResolvedEffect = resolvedEffect;
        }

        public RuntimeSynergyOutput Source { get; }
        public RuntimeContentIndex SourceIndex { get; }
        public RuntimeContentIndex TargetIndex { get; }
        internal IEffectExecutor EffectExecutor { get; }
        internal ResolvedEffectOp ResolvedEffect { get; }
    }

    public sealed class CompiledPassiveDefinition
    {
        internal CompiledPassiveDefinition(RuntimeContentIndex index, RuntimePassiveDefinition source)
        {
            Index = index;
            Source = source;
        }

        public RuntimeContentIndex Index { get; }
        public RuntimePassiveDefinition Source { get; }
    }

    public sealed class CompiledTraitDefinition
    {
        internal CompiledTraitDefinition(RuntimeContentIndex index, RuntimeTraitDefinition source)
        {
            Index = index;
            Source = source;
        }

        public RuntimeContentIndex Index { get; }
        public RuntimeTraitDefinition Source { get; }
    }

    public sealed class CompiledSynergyDefinition
    {
        internal CompiledSynergyDefinition(
            RuntimeContentIndex index,
            RuntimeSynergyDefinition source,
            CompiledBuildCondition[] conditions,
            CompiledSynergyOutput[] outputs)
        {
            Index = index;
            Source = source;
            Conditions = conditions;
            Outputs = outputs;
        }

        public RuntimeContentIndex Index { get; }
        public RuntimeSynergyDefinition Source { get; }
        public IReadOnlyList<CompiledBuildCondition> Conditions { get; }
        public IReadOnlyList<CompiledSynergyOutput> Outputs { get; }
    }

    public sealed class CompiledEvolutionDefinition
    {
        internal CompiledEvolutionDefinition(
            RuntimeContentIndex index,
            RuntimeEvolutionDefinition source,
            RuntimeContentIndex requiredSkillIndex,
            RuntimeContentIndex resultSkillIndex,
            RuntimeContentIndex[] requiredPassiveIndices,
            CompiledBuildCondition[] conditions)
        {
            Index = index;
            Source = source;
            RequiredSkillIndex = requiredSkillIndex;
            ResultSkillIndex = resultSkillIndex;
            RequiredPassiveIndices = requiredPassiveIndices;
            Conditions = conditions;
        }

        public RuntimeContentIndex Index { get; }
        public RuntimeEvolutionDefinition Source { get; }
        public RuntimeContentIndex RequiredSkillIndex { get; }
        public RuntimeContentIndex ResultSkillIndex { get; }
        public IReadOnlyList<RuntimeContentIndex> RequiredPassiveIndices { get; }
        public IReadOnlyList<CompiledBuildCondition> Conditions { get; }
    }

    public sealed class CompiledUpgradeOfferDefinition
    {
        internal CompiledUpgradeOfferDefinition(
            RuntimeContentIndex index,
            RuntimeUpgradeOfferDefinition source,
            UpgradeTargetKind targetKind,
            RuntimeContentIndex targetIndex,
            int targetMaximumLevel,
            CompiledBuildCondition[] prerequisites)
        {
            Index = index;
            Source = source;
            TargetKind = targetKind;
            TargetIndex = targetIndex;
            TargetMaximumLevel = targetMaximumLevel;
            Prerequisites = prerequisites;
        }

        public RuntimeContentIndex Index { get; }
        public RuntimeUpgradeOfferDefinition Source { get; }
        public UpgradeTargetKind TargetKind { get; }
        public RuntimeContentIndex TargetIndex { get; }
        public int TargetMaximumLevel { get; }
        public IReadOnlyList<CompiledBuildCondition> Prerequisites { get; }
    }

    /// <summary>
    /// Run-local compiled M6 content. Stable IDs and effect references are resolved before
    /// fixed-tick or offer generation paths begin.
    /// </summary>
    public sealed class BuildRuntimeCatalog
    {
        private readonly Dictionary<ContentId, CompiledPassiveDefinition> passivesById;
        private readonly Dictionary<ContentId, CompiledTraitDefinition> traitsById;
        private readonly Dictionary<ContentId, CompiledEvolutionDefinition> evolutionsById;
        private readonly Dictionary<ContentId, CompiledUpgradeOfferDefinition> offersById;
        private readonly Dictionary<ContentId, RuntimeContentDefinition> definitionsById;
        private readonly Dictionary<ContentId, RuntimeContentIndex> indicesById;
        private readonly RuntimeContentDefinition[] orderedDefinitions;

        private BuildRuntimeCatalog(
            CompiledPassiveDefinition[] passives,
            CompiledTraitDefinition[] traits,
            CompiledSynergyDefinition[] synergies,
            CompiledEvolutionDefinition[] evolutions,
            CompiledUpgradeOfferDefinition[] offers,
            Dictionary<ContentId, RuntimeContentDefinition> runtimeDefinitions,
            Dictionary<ContentId, RuntimeContentIndex> runtimeIndices,
            RuntimeContentDefinition[] definitions)
        {
            Passives = Array.AsReadOnly(passives);
            Traits = Array.AsReadOnly(traits);
            Synergies = Array.AsReadOnly(synergies);
            Evolutions = Array.AsReadOnly(evolutions);
            Offers = Array.AsReadOnly(offers);
            passivesById = Index(passives, value => value.Source.Id);
            traitsById = Index(traits, value => value.Source.Id);
            evolutionsById = Index(evolutions, value => value.Source.Id);
            offersById = Index(offers, value => value.Source.Id);
            definitionsById = runtimeDefinitions;
            indicesById = runtimeIndices;
            orderedDefinitions = definitions ?? Array.Empty<RuntimeContentDefinition>();
        }

        public IReadOnlyList<CompiledPassiveDefinition> Passives { get; }
        public IReadOnlyList<CompiledTraitDefinition> Traits { get; }
        public IReadOnlyList<CompiledSynergyDefinition> Synergies { get; }
        public IReadOnlyList<CompiledEvolutionDefinition> Evolutions { get; }
        public IReadOnlyList<CompiledUpgradeOfferDefinition> Offers { get; }

        public bool TryGetPassive(ContentId id, out CompiledPassiveDefinition definition) => passivesById.TryGetValue(id, out definition);
        public bool TryGetTrait(ContentId id, out CompiledTraitDefinition definition) => traitsById.TryGetValue(id, out definition);
        public bool TryGetEvolution(ContentId id, out CompiledEvolutionDefinition definition) => evolutionsById.TryGetValue(id, out definition);
        public bool TryGetOffer(ContentId id, out CompiledUpgradeOfferDefinition definition) => offersById.TryGetValue(id, out definition);
        public bool TryGetDefinition(ContentId id, out RuntimeContentDefinition definition) => definitionsById.TryGetValue(id, out definition);
        public bool TryGetIndex(ContentId id, out RuntimeContentIndex index) => indicesById.TryGetValue(id, out index);

        internal int DefinitionCount => orderedDefinitions.Length;

        internal RuntimeContentDefinition GetDefinitionAt(int index)
        {
            if (index < 0 || index >= orderedDefinitions.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return orderedDefinitions[index];
        }

        public static Result<BuildRuntimeCatalog> Build(
            ContentRegistry content,
            SkillModuleRegistry modules,
            StatCatalog stats = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            var statCatalog = stats ?? StatCatalog.Default;
            var passives = new List<CompiledPassiveDefinition>();
            var traits = new List<CompiledTraitDefinition>();
            var synergies = new List<CompiledSynergyDefinition>();
            var evolutions = new List<CompiledEvolutionDefinition>();
            var offers = new List<CompiledUpgradeOfferDefinition>();
            var definitionsById = new Dictionary<ContentId, RuntimeContentDefinition>(content.Count);
            var indicesById = new Dictionary<ContentId, RuntimeContentIndex>(content.Count);
            var orderedDefinitions = new RuntimeContentDefinition[content.Count];

            for (var index = 0; index < content.Count; index++)
            {
                var entryResult = content.Get(new RuntimeContentIndex(index));
                if (!entryResult.IsSuccess) return Result<BuildRuntimeCatalog>.Failure(entryResult.Error);
                var entry = entryResult.Value;
                definitionsById.Add(entry.Definition.Id, entry.Definition);
                indicesById.Add(entry.Definition.Id, entry.Index);
                orderedDefinitions[index] = entry.Definition;
                if (entry.Definition is RuntimePassiveDefinition passive)
                    passives.Add(new CompiledPassiveDefinition(entry.Index, passive));
                else if (entry.Definition is RuntimeTraitDefinition trait)
                    traits.Add(new CompiledTraitDefinition(entry.Index, trait));
                else if (entry.Definition is RuntimeSynergyDefinition synergy)
                {
                    var compiled = CompileSynergy(entry.Index, synergy, content, modules, statCatalog);
                    if (!compiled.IsSuccess) return Result<BuildRuntimeCatalog>.Failure(compiled.Error);
                    synergies.Add(compiled.Value);
                }
                else if (entry.Definition is RuntimeEvolutionDefinition evolution)
                {
                    var compiled = CompileEvolution(entry.Index, evolution, content, statCatalog);
                    if (!compiled.IsSuccess) return Result<BuildRuntimeCatalog>.Failure(compiled.Error);
                    evolutions.Add(compiled.Value);
                }
                else if (entry.Definition is RuntimeUpgradeOfferDefinition offer)
                {
                    var compiled = CompileOffer(entry.Index, offer, content, statCatalog);
                    if (!compiled.IsSuccess) return Result<BuildRuntimeCatalog>.Failure(compiled.Error);
                    offers.Add(compiled.Value);
                }
            }

            return Result<BuildRuntimeCatalog>.Success(
                new BuildRuntimeCatalog(
                    passives.ToArray(),
                    traits.ToArray(),
                    synergies.ToArray(),
                    evolutions.ToArray(),
                    offers.ToArray(),
                    definitionsById,
                    indicesById,
                    orderedDefinitions));
        }

        internal static BuildRuntimeCatalog Empty()
        {
            return new BuildRuntimeCatalog(
                Array.Empty<CompiledPassiveDefinition>(),
                Array.Empty<CompiledTraitDefinition>(),
                Array.Empty<CompiledSynergyDefinition>(),
                Array.Empty<CompiledEvolutionDefinition>(),
                Array.Empty<CompiledUpgradeOfferDefinition>(),
                new Dictionary<ContentId, RuntimeContentDefinition>(),
                new Dictionary<ContentId, RuntimeContentIndex>(),
                Array.Empty<RuntimeContentDefinition>());
        }

        private static Result<CompiledSynergyDefinition> CompileSynergy(
            RuntimeContentIndex index,
            RuntimeSynergyDefinition source,
            ContentRegistry content,
            SkillModuleRegistry modules,
            StatCatalog stats)
        {
            var conditions = CompileConditions(source.Conditions, source, stats);
            if (!conditions.IsSuccess) return Result<CompiledSynergyDefinition>.Failure(conditions.Error);
            var outputs = new CompiledSynergyOutput[source.Outputs.Count];
            for (var outputIndex = 0; outputIndex < source.Outputs.Count; outputIndex++)
            {
                var output = source.Outputs[outputIndex];
                var sourceRuntimeIndex = ResolveOptional(content, output.SourceId);
                var targetRuntimeIndex = ResolveOptional(content, output.TargetId);
                IEffectExecutor executor = null;
                var resolvedEffect = default(ResolvedEffectOp);
                if (output.Type == SynergyOutputType.AddEffectOp)
                {
                    if (!modules.TryGetEffect(output.Effect.Code, out executor))
                        return Failure<CompiledSynergyDefinition>(source, "Synergy effect executor is not registered.");
                    var statIndex = default(StatIndex);
                    if (output.Effect.StatId0.IsValid && !stats.TryGetIndex(output.Effect.StatId0, out statIndex))
                        return Failure<CompiledSynergyDefinition>(source, "Synergy effect references an unknown StatId.");
                    var reference0 = ResolveOptional(content, output.Effect.ReferenceId0);
                    var reference1 = ResolveOptional(content, output.Effect.ReferenceId1);
                    var bound = new EffectOp(
                        output.Effect.Code,
                        output.Effect.Value0,
                        output.Effect.Value1,
                        output.Effect.Value2,
                        output.Effect.Int0,
                        output.Effect.Int1,
                        output.Effect.ReferenceId0,
                        output.Effect.ReferenceId1,
                        output.Effect.Tag0,
                        output.Effect.StatId0,
                        output.Effect.Flags,
                        reference0,
                        reference1);
                    resolvedEffect = new ResolvedEffectOp(bound, statIndex);
                }
                outputs[outputIndex] = new CompiledSynergyOutput(output, sourceRuntimeIndex, targetRuntimeIndex, executor, resolvedEffect);
            }
            return Result<CompiledSynergyDefinition>.Success(new CompiledSynergyDefinition(index, source, conditions.Value, outputs));
        }

        private static Result<CompiledEvolutionDefinition> CompileEvolution(
            RuntimeContentIndex index,
            RuntimeEvolutionDefinition source,
            ContentRegistry content,
            StatCatalog stats)
        {
            if (!content.TryGet(source.RequiredSkillId, out var required) ||
                !content.TryGet(source.ResultSkillId, out var result))
                return Failure<CompiledEvolutionDefinition>(source, "Evolution skills could not be resolved.");
            var passives = new RuntimeContentIndex[source.RequiredPassiveIds.Count];
            for (var passiveIndex = 0; passiveIndex < passives.Length; passiveIndex++)
            {
                if (!content.TryGet(source.RequiredPassiveIds[passiveIndex], out var passive))
                    return Failure<CompiledEvolutionDefinition>(source, "Evolution passive could not be resolved.");
                passives[passiveIndex] = passive.Index;
            }
            var conditions = CompileConditions(source.AdditionalConditions, source, stats);
            if (!conditions.IsSuccess) return Result<CompiledEvolutionDefinition>.Failure(conditions.Error);
            return Result<CompiledEvolutionDefinition>.Success(
                new CompiledEvolutionDefinition(index, source, required.Index, result.Index, passives, conditions.Value));
        }

        private static Result<CompiledUpgradeOfferDefinition> CompileOffer(
            RuntimeContentIndex index,
            RuntimeUpgradeOfferDefinition source,
            ContentRegistry content,
            StatCatalog stats)
        {
            if (!content.TryGet(source.TargetContentId, out var target))
                return Failure<CompiledUpgradeOfferDefinition>(source, "Offer target could not be resolved.");
            UpgradeTargetKind kind;
            int maximumLevel;
            if (target.Definition is RuntimeSkillDefinition skill && skill.IsExecutable)
            {
                kind = UpgradeTargetKind.Skill;
                maximumLevel = skill.MaximumLevel;
            }
            else if (target.Definition is RuntimePassiveDefinition passive)
            {
                kind = UpgradeTargetKind.Passive;
                maximumLevel = passive.MaximumLevel;
            }
            else if (target.Definition is RuntimeEvolutionDefinition)
            {
                kind = UpgradeTargetKind.Evolution;
                maximumLevel = 1;
            }
            else
            {
                return Failure<CompiledUpgradeOfferDefinition>(source, "Offer target has an unsupported runtime kind.");
            }
            var conditions = CompileConditions(source.Prerequisites, source, stats);
            if (!conditions.IsSuccess) return Result<CompiledUpgradeOfferDefinition>.Failure(conditions.Error);
            return Result<CompiledUpgradeOfferDefinition>.Success(
                new CompiledUpgradeOfferDefinition(index, source, kind, target.Index, maximumLevel, conditions.Value));
        }

        private static Result<CompiledBuildCondition[]> CompileConditions(
            IReadOnlyList<BuildCondition> source,
            RuntimeContentDefinition owner,
            StatCatalog stats)
        {
            var output = new CompiledBuildCondition[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                var condition = source[index];
                var statIndex = default(StatIndex);
                if (condition.Type == BuildConditionType.StatAtLeast &&
                    !stats.TryGetIndex(condition.StatId, out statIndex))
                    return Failure<CompiledBuildCondition[]>(owner, "Build condition references an unknown StatId.");
                output[index] = new CompiledBuildCondition(condition, statIndex);
            }
            return Result<CompiledBuildCondition[]>.Success(output);
        }

        private static RuntimeContentIndex ResolveOptional(ContentRegistry content, ContentId id)
        {
            return id.IsValid && content.TryGet(id, out var entry) ? entry.Index : default;
        }

        private static Dictionary<ContentId, T> Index<T>(T[] source, Func<T, ContentId> getId)
        {
            var output = new Dictionary<ContentId, T>(source.Length);
            for (var index = 0; index < source.Length; index++) output.Add(getId(source[index]), source[index]);
            return output;
        }

        private static Result<T> Failure<T>(RuntimeContentDefinition source, string message)
        {
            return Result<T>.Failure(
                new Error(ErrorCode.InvalidAuthoringData, message, source.Id, default, source.SourceAssetPath));
        }
    }
}
