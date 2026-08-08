using System;
using Game.Content.Runtime;
using Game.Core;
using Game.Presentation;
using Game.Simulation;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Builds Development-only presentation profiles from generic content kinds,
    /// tags, delivery modules, and authored stable profile identities.
    /// </summary>
    public static class QinglanProceduralPresentationFactory
    {
        private static readonly ContentTag PlayerTag = Tag("character.player");
        private static readonly ContentTag SkillPlayerTag = Tag("skill.player");
        private static readonly ContentTag SkillEnemyTag = Tag("skill.enemy");
        private static readonly ContentTag SkillBossTag = Tag("skill.boss");
        private static readonly ContentTag BossHazardTag = Tag("damage.channel.boss_hazard");
        private static readonly ContentTag EnemyBossTag = Tag("enemy.boss");
        private static readonly ContentTag EnemyFlyingTag = Tag("enemy.flying");
        private static readonly ContentTag EnemySupportTag = Tag("enemy.support");
        private static readonly ContentTag EnemyExplosiveTag = Tag("enemy.explosive");

        public static ProceduralPresentationCatalog Build(ContentRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var catalog = new ProceduralPresentationCatalog(Math.Max(64, registry.Count * 2));
            for (var index = 0; index < registry.Count; index++)
            {
                var entry = registry.Get(new RuntimeContentIndex(index));
                if (!entry.IsSuccess) continue;
                var definition = entry.Value.Definition;
                var style = CreateStyle(definition);
                catalog.Register(definition.Id, style);

                if (definition is RuntimeEnemyDefinition enemy && enemy.VisualProfileId.IsValid)
                    catalog.Register(enemy.VisualProfileId, style);
                if (definition is RuntimeMapDefinition map && map.VisualProfileId.IsValid)
                    catalog.Register(map.VisualProfileId, style);
                if (definition is RuntimeSkillDefinition skill &&
                    skill.IsExecutable && skill.Delivery.PresentationId.IsValid)
                    catalog.Register(skill.Delivery.PresentationId, style);
                if (definition is RuntimeQinglanDefinition qinglan && qinglan.PresentationProfileId.IsValid)
                    catalog.Register(qinglan.PresentationProfileId, style);
            }
            return catalog;
        }

        private static ProceduralPresentationStyle CreateStyle(RuntimeContentDefinition definition)
        {
            var variant = ((uint)definition.Id.StableHash & 0xFFu) / 255f;
            if (definition is RuntimeCharacterDefinition || Has(definition, PlayerTag))
                return Style(
                    ProceduralShape.Triangle, Friendly(variant), new Color(0.96f, 0.94f, 0.84f, 1f),
                    1.1f, 1.25f, PresentationPriority.Mechanic, PresentationAudioCue.None, false, true);

            if (definition is RuntimeEnemyDefinition)
            {
                if (Has(definition, EnemyBossTag))
                    return Style(
                        ProceduralShape.Hexagon, Danger(variant, 1f), new Color(0.28f, 0.06f, 0.04f, 1f),
                        2.35f, 2.35f, PresentationPriority.CriticalDanger,
                        PresentationAudioCue.BossPhase, true, true);
                var shape = Has(definition, EnemyFlyingTag) ? ProceduralShape.Diamond :
                    Has(definition, EnemySupportTag) ? ProceduralShape.Cross :
                    Has(definition, EnemyExplosiveTag) ? ProceduralShape.Ring : ProceduralShape.Circle;
                var priority = Has(definition, EnemyExplosiveTag) ?
                    PresentationPriority.CriticalDanger : PresentationPriority.Combat;
                return Style(
                    shape, Enemy(variant), new Color(0.24f, 0.2f, 0.17f, 1f),
                    shape == ProceduralShape.Ring ? 1.35f : 1f,
                    shape == ProceduralShape.Ring ? 1.35f : 1f,
                    priority,
                    Has(definition, EnemyExplosiveTag) ? PresentationAudioCue.Danger : PresentationAudioCue.Hit,
                    true,
                    shape == ProceduralShape.Diamond);
            }

            if (definition is RuntimeSkillDefinition skill)
            {
                var enemy = Has(definition, SkillEnemyTag);
                var boss = Has(definition, SkillBossTag) || Has(definition, BossHazardTag);
                var area = skill.IsExecutable &&
                    (skill.Delivery.ModuleId == SkillModuleIds.DeliveryArea ||
                     skill.Delivery.ModuleId == SkillModuleIds.DeliveryAura);
                var outbound = skill.IsExecutable && skill.Delivery.ModuleId == SkillModuleIds.DeliveryOutboundReturn;
                var shape = area ? ProceduralShape.Ring : outbound ? ProceduralShape.Chevron :
                    boss ? ProceduralShape.Line : ProceduralShape.Diamond;
                var hostile = enemy || boss;
                var critical = boss || (enemy && (area || Has(definition, EnemyExplosiveTag)));
                return Style(
                    shape,
                    hostile ? Danger(variant, area ? 0.58f : 1f) : Friendly(variant),
                    hostile ? new Color(0.3f, 0.06f, 0.03f, 1f) : new Color(0.96f, 0.94f, 0.84f, 1f),
                    area ? 2.2f : 0.48f,
                    area ? 2.2f : outbound ? 0.65f : 0.48f,
                    critical ? PresentationPriority.CriticalDanger : PresentationPriority.Mechanic,
                    critical ? PresentationAudioCue.Danger : PresentationAudioCue.Hit,
                    hostile,
                    hostile || outbound);
            }

            if (definition is RuntimeEliteAffixDefinition)
            {
                var shapes = new[]
                {
                    ProceduralShape.Ring, ProceduralShape.Cross,
                    ProceduralShape.Chevron, ProceduralShape.Hexagon
                };
                return Style(
                    shapes[Math.Abs(definition.Id.StableHash % shapes.Length)],
                    new Color(0.94f, 0.56f + (0.18f * variant), 0.16f, 0.85f),
                    new Color(0.2f, 0.05f, 0.02f, 1f),
                    1.05f, 1.05f, PresentationPriority.Mechanic,
                    PresentationAudioCue.Danger, true, true);
            }

            if (definition is RuntimeCharacterMechanicDefinition)
                return Style(
                    ProceduralShape.Ring, Friendly(variant), Color.white,
                    1.45f, 1.45f, PresentationPriority.Mechanic,
                    PresentationAudioCue.MechanicRise, false, true);

            if (definition is RuntimePickupDefinition || definition is RuntimeRewardDefinition ||
                definition is RuntimeRelicDefinition)
                return Style(
                    definition is RuntimePickupDefinition ? ProceduralShape.Cross : ProceduralShape.Diamond,
                    Reward(variant), new Color(1f, 0.95f, 0.7f, 1f),
                    0.55f, 0.55f, PresentationPriority.Mechanic,
                    PresentationAudioCue.Pickup, false);

            if (definition is RuntimeMapObjectiveDefinition || definition is RuntimeMapEventDefinition ||
                definition is RuntimeLandmarkDefinition || definition is RuntimeBossDefinition)
                return Style(
                    definition is RuntimeMapEventDefinition ? ProceduralShape.Chevron :
                    definition is RuntimeLandmarkDefinition ? ProceduralShape.Diamond : ProceduralShape.Ring,
                    definition is RuntimeBossDefinition ? Danger(variant, 0.9f) : Friendly(variant),
                    Color.white, 1.2f, 1.2f,
                    definition is RuntimeBossDefinition ? PresentationPriority.CriticalDanger : PresentationPriority.Mechanic,
                    definition is RuntimeBossDefinition ? PresentationAudioCue.BossPhase : PresentationAudioCue.Objective,
                    definition is RuntimeBossDefinition, true);

            if (definition is RuntimeMapDefinition)
                return Style(
                    ProceduralShape.Square, new Color(0.47f, 0.62f, 0.52f, 0.28f),
                    new Color(0.72f, 0.62f, 0.47f, 0.7f), 1f, 1f,
                    PresentationPriority.Decoration, PresentationAudioCue.None, false);

            if (definition is RuntimeStatusDefinition)
                return Style(
                    ProceduralShape.Ring, new Color(0.5f + (variant * 0.25f), 0.45f, 0.9f, 0.65f),
                    Color.white, 0.75f, 0.75f,
                    PresentationPriority.Combat, PresentationAudioCue.None, false);

            return ProceduralPresentationCatalog.Fallback(EntityKind.Actor, false);
        }

        private static ProceduralPresentationStyle Style(
            ProceduralShape shape,
            Color color,
            Color outline,
            float width,
            float height,
            PresentationPriority priority,
            PresentationAudioCue audio,
            bool hostile,
            bool directional = false) =>
            new ProceduralPresentationStyle(
                shape, color, outline, new Vector2(width, height), priority, audio, hostile, directional);

        private static Color Friendly(float variant) =>
            new Color(0.26f + (0.12f * variant), 0.72f + (0.12f * variant), 0.74f, 1f);

        private static Color Enemy(float variant) =>
            new Color(0.34f + (0.08f * variant), 0.43f + (0.12f * variant), 0.28f, 1f);

        private static Color Danger(float variant, float alpha) =>
            new Color(0.82f + (0.12f * variant), 0.24f + (0.12f * variant), 0.15f, alpha);

        private static Color Reward(float variant) =>
            new Color(0.76f + (0.18f * variant), 0.61f + (0.18f * variant), 0.22f, 1f);

        private static bool Has(RuntimeContentDefinition definition, ContentTag tag)
        {
            for (var index = 0; index < definition.Tags.Count; index++)
                if (definition.Tags[index] == tag) return true;
            return false;
        }

        private static ContentTag Tag(string value) => ContentTag.Create(value).Value;
    }
}
