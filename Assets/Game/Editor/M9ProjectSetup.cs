using System;
using Game.Content.Authoring;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates the small wizard-generated M9 coverage and extensibility pack.</summary>
    public static class M9ProjectSetup
    {
        /// <summary>Gets the root folder for generated M9 Placeholder content.</summary>
        public const string RootFolder = "Assets/GameAssets/Placeholder/M9EditorTools";
        /// <summary>Gets the generated coverage-pack folder.</summary>
        public const string PackFolder = RootFolder + "/m9_tools";
        /// <summary>Gets the generated coverage-pack authoring path.</summary>
        public const string PackPath = PackFolder + "/M9ToolsContentPack.asset";
        /// <summary>Gets the generated coverage-pack catalog path.</summary>
        public const string BakedCatalogPath = PackFolder + "/M9ToolsContentPack.baked.json";

        /// <summary>Creates or refreshes every M9 wizard coverage fixture.</summary>
        [MenuItem("Tools/Free World/M9/Configure Wizard Coverage Content")]
        public static void Configure()
        {
            var packResult = Create(ContentCreationKind.Pack, "M9 Tools", null);
            var pack = RequireResult<ContentPackAuthoring>(packResult);
            var skill = RequireResult<SkillAuthoring>(
                Create(ContentCreationKind.Skill, "Second", pack));
            RequireResult<CharacterAuthoring>(
                Create(ContentCreationKind.Character, "Second", pack, skill: skill));
            var passive = RequireResult<PassiveAuthoring>(
                Create(ContentCreationKind.Passive, "M9", pack));
            RequireResult<TraitAuthoring>(
                Create(ContentCreationKind.Trait, "M9", pack));
            RequireResult<StatusEffectAuthoring>(
                Create(ContentCreationKind.Status, "M9", pack));
            var enemy = RequireResult<EnemyAuthoring>(
                Create(ContentCreationKind.Enemy, "M9", pack, skill: skill));
            var encounter = RequireResult<EncounterScheduleAuthoring>(
                Create(ContentCreationKind.Encounter, "M9", pack, enemy: enemy));
            RequireResult<MapAuthoring>(
                Create(ContentCreationKind.Map, "Second", pack, encounter: encounter));
            RequireResult<SynergyAuthoring>(
                Create(ContentCreationKind.Synergy, "M9", pack));

            var evolved = AssetDatabase.LoadAssetAtPath<SkillAuthoring>(
                M4TestSkillSetup.Folder + "/TestGroundArea.asset");
            if (evolved == null) throw new UnityException("M4 Ground Area fixture is missing.");
            RequireResult<EvolutionAuthoring>(
                Create(
                    ContentCreationKind.Evolution,
                    "M9",
                    pack,
                    skill,
                    evolved,
                    passive));

            var validation = ProjectGovernanceValidator.ValidateCurrentProject();
            if (!validation.IsValid) throw new UnityException(validation.Issues[0].ToString());
            Debug.Log("[M9 Setup] Wizard coverage pack configured: definitions=" +
                      pack.Definitions.Count + ".");
        }

        /// <summary>Runs the deterministic setup from batch mode.</summary>
        public static void RunFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                Configure();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        private static ContentCreationResult Create(
            ContentCreationKind kind,
            string name,
            ContentPackAuthoring pack,
            SkillAuthoring skill = null,
            SkillAuthoring secondarySkill = null,
            PassiveAuthoring passive = null,
            EnemyAuthoring enemy = null,
            EncounterScheduleAuthoring encounter = null)
        {
            return ContentCreationService.Create(new ContentCreationRequest
            {
                Kind = kind,
                NamespaceId = "test",
                DisplayName = name,
                RootFolder = RootFolder,
                Pack = pack,
                OverwriteExisting = true,
                SkillReference = skill,
                SecondarySkillReference = secondarySkill,
                PassiveReference = passive,
                EnemyReference = enemy,
                EncounterReference = encounter
            });
        }

        private static T RequireResult<T>(ContentCreationResult result)
            where T : UnityEngine.Object
        {
            if (!(result.Asset is T typed))
                throw new UnityException("Wizard returned " + result.Asset + " instead of " + typeof(T).Name + ".");
            return typed;
        }
    }
}
