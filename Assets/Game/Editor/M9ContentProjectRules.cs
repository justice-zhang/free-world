using System;
using System.Collections.Generic;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace Game.Editor
{
    /// <summary>Locates SpawnSecondarySkill trigger-chain cycles with stable ID paths.</summary>
    public static class TriggerChainValidator
    {
        /// <summary>Returns deterministic cycle descriptions for executable skills.</summary>
        public static IReadOnlyList<string> FindCycles(
            IReadOnlyList<RuntimeSkillDefinition> skills)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            var byId = new Dictionary<ContentId, RuntimeSkillDefinition>(skills.Count);
            for (var index = 0; index < skills.Count; index++)
            {
                if (skills[index] != null) byId[skills[index].Id] = skills[index];
            }

            var states = new Dictionary<ContentId, VisitState>(byId.Count);
            var stack = new List<ContentId>();
            var cycles = new List<string>();
            var ordered = new List<ContentId>(byId.Keys);
            ordered.Sort((left, right) => string.CompareOrdinal(left.Value, right.Value));
            for (var index = 0; index < ordered.Count; index++)
                Visit(ordered[index], byId, states, stack, cycles);
            return cycles;
        }

        internal static void Append(
            IReadOnlyList<BakedContentCatalog> catalogs,
            ValidationReport report)
        {
            var skills = new List<RuntimeSkillDefinition>();
            for (var packIndex = 0; packIndex < catalogs.Count; packIndex++)
            {
                for (var definitionIndex = 0;
                     definitionIndex < catalogs[packIndex].Definitions.Count;
                     definitionIndex++)
                {
                    if (catalogs[packIndex].Definitions[definitionIndex] is RuntimeSkillDefinition skill &&
                        skill.IsExecutable)
                        skills.Add(skill);
                }
            }

            var cycles = FindCycles(skills);
            for (var index = 0; index < cycles.Count; index++)
                report.Add("M9-TRIGGER-CYCLE", "SpawnSecondarySkill cycle: " + cycles[index] + ".");
        }

        private static void Visit(
            ContentId id,
            Dictionary<ContentId, RuntimeSkillDefinition> byId,
            Dictionary<ContentId, VisitState> states,
            List<ContentId> stack,
            List<string> cycles)
        {
            if (states.TryGetValue(id, out var existing))
            {
                if (existing == VisitState.Visiting) AddCycle(id, stack, cycles);
                return;
            }

            states[id] = VisitState.Visiting;
            stack.Add(id);
            var skill = byId[id];
            for (var effectIndex = 0; effectIndex < skill.Effects.Count; effectIndex++)
            {
                var effect = skill.Effects[effectIndex];
                if (effect.Code != EffectOpCode.SpawnSecondarySkill ||
                    !effect.ReferenceId0.IsValid ||
                    !byId.ContainsKey(effect.ReferenceId0))
                    continue;
                Visit(effect.ReferenceId0, byId, states, stack, cycles);
            }

            stack.RemoveAt(stack.Count - 1);
            states[id] = VisitState.Visited;
        }

        private static void AddCycle(
            ContentId repeated,
            List<ContentId> stack,
            List<string> cycles)
        {
            var start = 0;
            while (start < stack.Count && stack[start] != repeated) start++;
            var builder = new System.Text.StringBuilder();
            for (var index = start; index < stack.Count; index++)
            {
                if (builder.Length > 0) builder.Append(" -> ");
                builder.Append(stack[index].Value);
            }

            if (builder.Length > 0) builder.Append(" -> ");
            builder.Append(repeated.Value);
            var value = builder.ToString();
            if (!cycles.Contains(value)) cycles.Add(value);
        }

        private enum VisitState : byte
        {
            Visiting = 1,
            Visited = 2
        }
    }

    internal static class VisualProfileProjectValidator
    {
        internal static void Append(
            IReadOnlyList<BakedContentCatalog> catalogs,
            AddressableAssetSettings settings,
            ValidationReport report)
        {
            for (var packIndex = 0; packIndex < catalogs.Count; packIndex++)
            {
                var definitions = catalogs[packIndex].Definitions;
                for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
                {
                    var definition = definitions[definitionIndex];
                    if (definition is RuntimeEnemyDefinition enemy && enemy.VisualProfileId.IsValid)
                        Validate(definition, enemy.VisualProfileId, settings, report);
                    else if (definition is RuntimeMapDefinition map && map.VisualProfileId.IsValid)
                        Validate(definition, map.VisualProfileId, settings, report);
                    else if (definition is RuntimeSkillDefinition skill && skill.IsExecutable &&
                             skill.Delivery.PresentationId.IsValid)
                        Validate(definition, skill.Delivery.PresentationId, settings, report);
                }
            }
        }

        private static void Validate(
            RuntimeContentDefinition owner,
            ContentId profileId,
            AddressableAssetSettings settings,
            ValidationReport report)
        {
            var placeholderId = profileId.Value.StartsWith("placeholder.", StringComparison.Ordinal);
            var placeholderContent = HasTag(owner, "content.placeholder");
            if (placeholderId)
            {
                if (!placeholderContent)
                {
                    report.Add(
                        "M9-VISUAL-PROFILE-PLACEHOLDER",
                        owner.Id + " uses " + profileId + " without content.placeholder tag at " +
                        owner.SourceAssetPath + ".");
                }

                return;
            }

            if (!AddressExists(settings, profileId.Value))
            {
                report.Add(
                    "M9-VISUAL-PROFILE-MISSING",
                    owner.Id + " cannot resolve Visual/Presentation Profile '" + profileId +
                    "' at " + owner.SourceAssetPath + ".");
            }
        }

        private static bool HasTag(RuntimeContentDefinition definition, string value)
        {
            for (var index = 0; index < definition.Tags.Count; index++)
                if (string.Equals(definition.Tags[index].Value, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool AddressExists(AddressableAssetSettings settings, string address)
        {
            if (settings == null) return false;
            for (var groupIndex = 0; groupIndex < settings.groups.Count; groupIndex++)
            {
                var group = settings.groups[groupIndex];
                if (group == null) continue;
                foreach (var entry in group.entries)
                    if (string.Equals(entry.address, address, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
