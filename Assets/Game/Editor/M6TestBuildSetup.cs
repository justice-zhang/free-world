using System;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates configuration-only Placeholder fixtures for M6 progression.</summary>
    public static class M6TestBuildSetup
    {
        public const string Folder = "Assets/GameAssets/Placeholder/TestBuildContent";
        public const string PackPath = Folder + "/TestM6BuildContentPack.asset";
        public const string BakedCatalogPath = Folder + "/TestM6BuildContentPack.baked.json";

        [MenuItem("Tools/Free World/M6/Configure Test Build Content")]
        public static void Configure()
        {
            EnsureFolder("Assets/GameAssets/Placeholder", "TestBuildContent");
            var single = Require<SkillAuthoring>(M4TestSkillSetup.Folder + "/TestSingleProjectile.asset");
            var orbit = Require<SkillAuthoring>(M4TestSkillSetup.Folder + "/TestOrbit.asset");
            var area = Require<SkillAuthoring>(M4TestSkillSetup.Folder + "/TestGroundArea.asset");
            var aura = Require<SkillAuthoring>(M4TestSkillSetup.Folder + "/TestDamageAura.asset");

            var force = LoadOrCreate<PassiveAuthoring>(Folder + "/TestForcePassive.asset");
            Identity(force, "test.passive.force", "build.force");
            force.Configure(3, new[]
            {
                PassiveModifier(1, "base.stat.damage", ModifierOperation.AddPercent, 0.1f),
                PassiveModifier(2, "base.stat.damage", ModifierOperation.AddPercent, 0.1f),
                PassiveModifier(3, "base.stat.damage", ModifierOperation.AddPercent, 0.1f)
            });

            var reach = LoadOrCreate<PassiveAuthoring>(Folder + "/TestReachPassive.asset");
            Identity(reach, "test.passive.reach", "build.reach");
            reach.Configure(2, new[]
            {
                PassiveModifier(1, "base.stat.pickup_range", ModifierOperation.AddFlat, 1f),
                PassiveModifier(2, "base.stat.pickup_range", ModifierOperation.AddFlat, 1f)
            });

            var prepared = LoadOrCreate<TraitAuthoring>(Folder + "/TestPreparedTrait.asset");
            Identity(prepared, "test.trait.prepared", "build.prepared");
            prepared.Configure(new[] { Modifier("base.stat.luck", ModifierOperation.AddFlat, 1f) });

            var evolution = LoadOrCreate<EvolutionAuthoring>(Folder + "/TestProjectileEvolution.asset");
            Identity(evolution, "test.evolution.projectile_area", "build.evolution");
            evolution.Configure(
                single,
                2,
                new[] { force },
                Array.Empty<BuildConditionAuthoringData>(),
                area,
                EvolutionConsumePolicy.ConsumeRequiredPassives);

            var singleOffer = Offer("TestSingleOffer", "test.offer.single", single, 4f, true);
            var orbitOffer = Offer("TestOrbitOffer", "test.offer.orbit", orbit, 2f, true);
            var forceOffer = Offer("TestForceOffer", "test.offer.force", force, 3f, true);
            var reachOffer = Offer("TestReachOffer", "test.offer.reach", reach, 2f, true);
            var evolutionOffer = Offer("TestEvolutionOffer", "test.offer.evolution", evolution, 5f, false);

            var synergyOne = LoadOrCreate<SynergyAuthoring>(Folder + "/TestForceSynergy.asset");
            Identity(synergyOne, "test.synergy.force", "build.synergy.force");
            synergyOne.Configure(
                new[] { TagCount("build.force", 1) },
                new[]
                {
                    new SynergyOutputAuthoringData
                    {
                        type = SynergyOutputType.AddModifier,
                        modifier = Modifier("base.stat.range", ModifierOperation.AddPercent, 0.15f)
                    },
                    new SynergyOutputAuthoringData
                    {
                        type = SynergyOutputType.UnlockOffer,
                        targetContent = evolutionOffer
                    },
                    new SynergyOutputAuthoringData
                    {
                        type = SynergyOutputType.AddEffectOp,
                        sourceContent = single,
                        effect = new SkillEffectAuthoringData
                        {
                            moduleId = "base.effect.damage",
                            value0 = 2f,
                            value1 = 1f,
                            int0 = (int)DamageType.Physical,
                            int1 = (int)DamageTags.Direct
                        }
                    }
                });

            var synergyTwo = LoadOrCreate<SynergyAuthoring>(Folder + "/TestReachOrbitSynergy.asset");
            Identity(synergyTwo, "test.synergy.reach_orbit", "build.synergy.reach_orbit");
            synergyTwo.Configure(
                new[]
                {
                    Owns(reach),
                    Owns(orbit),
                    MapTag("map.finite")
                },
                new[]
                {
                    new SynergyOutputAuthoringData
                    {
                        type = SynergyOutputType.TransformSkill,
                        sourceContent = orbit,
                        targetContent = aura
                    },
                    new SynergyOutputAuthoringData
                    {
                        type = SynergyOutputType.GrantTrait,
                        targetContent = prepared
                    }
                });

            var definitions = new ContentAuthoringBase[]
            {
                force,
                reach,
                prepared,
                evolution,
                singleOffer,
                orbitOffer,
                forceOffer,
                reachOffer,
                evolutionOffer,
                synergyOne,
                synergyTwo
            };
            var pack = LoadOrCreate<ContentPackAuthoring>(PackPath);
            pack.Configure(
                "test.pack.m6_build",
                "0.1.0",
                ContentPackTopology.BuildProgressionSchemaVersion,
                "0.1.0",
                string.Empty,
                new[]
                {
                    new ContentPackDependencyAuthoring
                    {
                        packId = "test.pack.m4_skills",
                        minimumVersion = "0.1.0",
                        maximumVersion = "0.1.0"
                    }
                },
                "packs/test.m6_build/catalog",
                "pack.test.m6_build",
                false,
                definitions);

            for (var index = 0; index < definitions.Length; index++) EditorUtility.SetDirty(definitions[index]);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            var bake = ContentBakeUtility.Bake(pack);
            if (!bake.IsSuccess) throw new UnityException(bake.Error.ToString());
            ContentBakeUtility.WriteCatalog(PackPath, bake.Value);
            AssetDatabase.SaveAssets();
            Debug.Log("[M6 Setup] Test build pack baked: entries=" + bake.Value.Definitions.Count +
                      ", hash=" + bake.Value.ContentHash + ".");
        }

        public static void RunFromCommandLine()
        {
            try
            {
                Configure();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static UpgradeOfferAuthoring Offer(
            string file,
            string id,
            ContentAuthoringBase target,
            float weight,
            bool unlocked)
        {
            var offer = LoadOrCreate<UpgradeOfferAuthoring>(Folder + "/" + file + ".asset");
            Identity(offer, id, "build.offer");
            offer.Configure(target, weight, unlocked, Array.Empty<BuildConditionAuthoringData>(), Array.Empty<ContentAuthoringBase>());
            return offer;
        }

        private static PassiveLevelModifierAuthoringData PassiveModifier(
            int level,
            string stat,
            ModifierOperation operation,
            float value) =>
            new PassiveLevelModifierAuthoringData
            {
                level = level,
                modifier = Modifier(stat, operation, value)
            };

        private static BuildModifierAuthoringData Modifier(
            string stat,
            ModifierOperation operation,
            float value) =>
            new BuildModifierAuthoringData
            {
                statId = stat,
                operation = operation,
                value = value
            };

        private static BuildConditionAuthoringData TagCount(string tag, int count) =>
            new BuildConditionAuthoringData
            {
                type = BuildConditionType.HasTagCount,
                tag = tag,
                integerValue = count
            };

        private static BuildConditionAuthoringData Owns(ContentAuthoringBase content) =>
            new BuildConditionAuthoringData
            {
                type = BuildConditionType.OwnsContent,
                content = content
            };

        private static BuildConditionAuthoringData MapTag(string tag) =>
            new BuildConditionAuthoringData
            {
                type = BuildConditionType.MapHasTag,
                tag = tag
            };

        private static void Identity(ContentAuthoringBase content, string id, string tag)
        {
            content.ConfigureIdentity(
                id,
                "content." + id + ".name",
                "content." + id + ".description",
                new[] { "content.placeholder", tag });
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) throw new UnityException("Required M4 fixture is missing: " + path);
            return value;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
