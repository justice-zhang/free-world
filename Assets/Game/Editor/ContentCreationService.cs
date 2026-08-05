using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Content.Authoring;
using Game.Content.Runtime;
using Game.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Tables;

namespace Game.Editor
{
    /// <summary>Content types supported by the M9 creation wizard.</summary>
    public enum ContentCreationKind : byte
    {
        Pack = 1,
        Character = 2,
        Skill = 3,
        Passive = 4,
        Trait = 5,
        Enemy = 6,
        Status = 7,
        Evolution = 8,
        Synergy = 9,
        Map = 10,
        Encounter = 11
    }

    /// <summary>Inputs accepted by both the wizard window and deterministic fixture setup.</summary>
    public sealed class ContentCreationRequest
    {
        /// <summary>Gets or sets the authoring type to create.</summary>
        public ContentCreationKind Kind { get; set; }
        /// <summary>Gets or sets the stable-ID namespace.</summary>
        public string NamespaceId { get; set; } = "test";
        /// <summary>Gets or sets the human-readable name used to derive the ID and filename.</summary>
        public string DisplayName { get; set; } = "new content";
        /// <summary>Gets or sets the project-relative root for generated assets.</summary>
        public string RootFolder { get; set; } =
            "Assets/GameAssets/Placeholder/GeneratedContent";
        /// <summary>Gets or sets the owning pack for non-pack content.</summary>
        public ContentPackAuthoring Pack { get; set; }
        /// <summary>Gets or sets whether an existing matching asset may be updated.</summary>
        public bool OverwriteExisting { get; set; }
        /// <summary>Gets or sets the primary skill reference used by applicable defaults.</summary>
        public SkillAuthoring SkillReference { get; set; }
        /// <summary>Gets or sets the secondary skill reference used by applicable defaults.</summary>
        public SkillAuthoring SecondarySkillReference { get; set; }
        /// <summary>Gets or sets the passive reference used by applicable defaults.</summary>
        public PassiveAuthoring PassiveReference { get; set; }
        /// <summary>Gets or sets the enemy reference used by encounter defaults.</summary>
        public EnemyAuthoring EnemyReference { get; set; }
        /// <summary>Gets or sets the encounter reference used by map defaults.</summary>
        public EncounterScheduleAuthoring EncounterReference { get; set; }
    }

    /// <summary>Audit data returned after one successful wizard operation.</summary>
    public sealed class ContentCreationResult
    {
        internal ContentCreationResult(
            UnityEngine.Object asset,
            string assetPath,
            string generatedId,
            string nameKey,
            string descriptionKey,
            string testTemplatePath,
            string sourceRecordPath)
        {
            Asset = asset;
            AssetPath = assetPath;
            GeneratedId = generatedId;
            NameKey = nameKey;
            DescriptionKey = descriptionKey;
            TestTemplatePath = testTemplatePath;
            SourceRecordPath = sourceRecordPath;
        }

        /// <summary>Gets the created or updated authoring object.</summary>
        public UnityEngine.Object Asset { get; }
        /// <summary>Gets the project-relative authoring asset path.</summary>
        public string AssetPath { get; }
        /// <summary>Gets the generated stable content or pack ID.</summary>
        public string GeneratedId { get; }
        /// <summary>Gets the generated localized-name key.</summary>
        public string NameKey { get; }
        /// <summary>Gets the generated localized-description key.</summary>
        public string DescriptionKey { get; }
        /// <summary>Gets the generated content test-template path.</summary>
        public string TestTemplatePath { get; }
        /// <summary>Gets the generated placeholder provenance-record path.</summary>
        public string SourceRecordPath { get; }
    }

    /// <summary>Creates safe defaults, adds them to a pack, and updates all authoring metadata.</summary>
    public static class ContentCreationService
    {
        /// <summary>Creates or deterministically updates one wizard asset.</summary>
        public static ContentCreationResult Create(ContentCreationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!Enum.IsDefined(typeof(ContentCreationKind), request.Kind))
                throw new ArgumentOutOfRangeException(nameof(request.Kind));
            var namespaceId = SanitizeIdentifier(request.NamespaceId);
            var slug = SanitizeIdentifier(request.DisplayName);
            if (string.IsNullOrEmpty(namespaceId) || string.IsNullOrEmpty(slug))
                throw new InvalidOperationException("Namespace and display name must contain letters or digits.");

            return request.Kind == ContentCreationKind.Pack
                ? CreatePack(request, namespaceId, slug)
                : CreateDefinition(request, namespaceId, slug);
        }

        private static ContentCreationResult CreatePack(
            ContentCreationRequest request,
            string namespaceId,
            string slug)
        {
            var root = NormalizeAssetFolder(request.RootFolder);
            EnsureAssetFolder(root);
            var folder = root + "/" + slug;
            EnsureAssetFolder(folder);
            var id = namespaceId + ".pack." + slug;
            var path = folder + "/" + PascalCase(slug) + "ContentPack.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(path);
            if (existing != null && !request.OverwriteExisting)
                throw new InvalidOperationException("A content pack already exists at " + path + ".");
            EnsurePackIdIsUnique(id, existing);
            var pack = existing ?? ScriptableObject.CreateInstance<ContentPackAuthoring>();
            if (existing == null) AssetDatabase.CreateAsset(pack, path);
            var definitions = CopyDefinitions(pack.Definitions);
            var dependencies = CopyDependencies(pack.Dependencies);
            var label = "pack." + namespaceId + "." + slug;
            pack.Configure(
                id,
                "0.1.0",
                ContentPackTopology.BuildProgressionSchemaVersion,
                "0.1.0",
                string.Empty,
                dependencies,
                "packs/" + id + "/catalog",
                label,
                false,
                definitions);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            M9AddressableUtility.Configure(path, id, label);
            var bakedPath = BakeAndLabel(pack, path);
            var testPath = WriteTestTemplate(
                folder,
                id,
                "pack",
                path,
                Array.Empty<string>());
            var sourcePath = WriteSourceRecord(pack, folder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Selection.activeObject = pack;
            Debug.Log("[M9 Wizard] Created pack " + id + " at " + path +
                      "; catalog=" + bakedPath + ".");
            return new ContentCreationResult(
                pack,
                path,
                id,
                string.Empty,
                string.Empty,
                testPath,
                sourcePath);
        }

        private static ContentCreationResult CreateDefinition(
            ContentCreationRequest request,
            string namespaceId,
            string slug)
        {
            var pack = request.Pack;
            if (pack == null) throw new InvalidOperationException("Select a target Content Pack first.");
            if (pack.SchemaVersion < ContentPackTopology.BuildProgressionSchemaVersion)
                throw new InvalidOperationException("M9 wizard content requires a schema-5 pack.");
            var packPath = AssetDatabase.GetAssetPath(pack);
            if (string.IsNullOrEmpty(packPath)) throw new InvalidOperationException("Target pack is not an asset.");
            var packFolder = (Path.GetDirectoryName(packPath) ?? "Assets").Replace('\\', '/');
            var kindToken = KindToken(request.Kind);
            var id = namespaceId + "." + kindToken + "." + slug;
            var folder = packFolder + "/Content/" + KindFolder(request.Kind);
            EnsureAssetFolder(folder);
            var path = folder + "/" + PascalCase(slug) + ".asset";
            var asset = LoadOrCreateDefinition(request.Kind, path, request.OverwriteExisting);
            EnsureDefinitionIdIsUnique(id, asset);
            var nameKey = "content." + id + ".name";
            var descriptionKey = "content." + id + ".description";
            asset.ConfigureIdentity(
                id,
                nameKey,
                descriptionKey,
                BuildTags(request.Kind));
            var references = ConfigureDefaults(asset, request);
            AddDefinitionAndDependencies(pack, asset, references);
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            M9LocalizationUtility.EnsureContentEntries(
                nameKey,
                descriptionKey,
                request.DisplayName);
            M9AddressableUtility.Configure(path, id, pack.AssetLabel);
            BakeAndLabel(pack, packPath);
            var testPath = WriteTestTemplate(
                packFolder,
                id,
                kindToken,
                path,
                new[] { nameKey, descriptionKey });
            var sourcePath = WriteSourceRecord(pack, packFolder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Selection.activeObject = asset;
            Debug.Log("[M9 Wizard] Created " + request.Kind + " " + id + " at " + path + ".");
            return new ContentCreationResult(
                asset,
                path,
                id,
                nameKey,
                descriptionKey,
                testPath,
                sourcePath);
        }

        private static List<ContentAuthoringBase> ConfigureDefaults(
            ContentAuthoringBase asset,
            ContentCreationRequest request)
        {
            var references = new List<ContentAuthoringBase>();
            if (asset is CharacterAuthoring character)
            {
                var skill = request.SkillReference ?? FindFirst<SkillAuthoring>(value => value.ModularRuntimeEnabled);
                character.Configure(100f, 5f, skill == null ? Array.Empty<SkillAuthoring>() : new[] { skill });
                AddReference(references, skill);
            }
            else if (asset is SkillAuthoring skill)
            {
                skill.ConfigureRuntime(
                    1f,
                    0f,
                    Module("base.trigger.timer"),
                    Module("base.condition.always"),
                    Module("base.targeting.nearest", 8f, int0: 1),
                    Module("base.delivery.instant"),
                    new[]
                    {
                        new SkillEffectAuthoringData
                        {
                            moduleId = "base.effect.damage",
                            value0 = 10f,
                            value1 = 1f,
                            int0 = (int)DamageType.Physical,
                            int1 = (int)DamageTags.Direct
                        }
                    },
                    new[]
                    {
                        new SkillLevelPatchAuthoringData
                        {
                            level = 2,
                            path = "effects[0].value0",
                            valueType = SkillPatchValueType.Float,
                            operation = SkillPatchOperation.Add,
                            floatValue = 5f
                        }
                    });
            }
            else if (asset is PassiveAuthoring passive)
            {
                passive.Configure(
                    2,
                    new[]
                    {
                        PassiveModifier(1, 0.1f),
                        PassiveModifier(2, 0.1f)
                    });
            }
            else if (asset is TraitAuthoring trait)
            {
                trait.Configure(new[] { Modifier("base.stat.luck", 1f) });
            }
            else if (asset is EnemyAuthoring enemyAuthoring)
            {
                var attack = request.SkillReference ?? FindFirst<SkillAuthoring>(value => value.ModularRuntimeEnabled);
                if (attack == null) throw new InvalidOperationException("Create or select an executable Skill before an Enemy.");
                enemyAuthoring.ConfigureM5(
                    30f,
                    0.5f,
                    2f,
                    4f,
                    6f,
                    attack,
                    3f,
                    0f,
                    "placeholder.visual." + asset.ContentIdText,
                    EnemyMovementMode.Chase,
                    1f,
                    0.1f,
                    0.4f,
                    0.5f,
                    2f,
                    1f,
                    1f,
                    0.5f,
                    1f);
                AddReference(references, attack);
            }
            else if (asset is StatusEffectAuthoring status)
            {
                status.Configure(
                    StatusStackingPolicy.RefreshDuration,
                    5f,
                    1,
                    0f,
                    new[] { "status.negative" },
                    Array.Empty<string>());
                status.ConfigureBehavior(default, default, 0f);
            }
            else if (asset is EvolutionAuthoring evolution)
            {
                var source = request.SkillReference ?? FindFirst<SkillAuthoring>(value => value.ModularRuntimeEnabled);
                var result = request.SecondarySkillReference ??
                             FindFirst<SkillAuthoring>(value => value.ModularRuntimeEnabled && value != source);
                if (source == null || result == null || source == result)
                    throw new InvalidOperationException("Evolution requires two distinct executable Skills.");
                var passives = request.PassiveReference == null
                    ? Array.Empty<PassiveAuthoring>()
                    : new[] { request.PassiveReference };
                evolution.Configure(
                    source,
                    1,
                    passives,
                    Array.Empty<BuildConditionAuthoringData>(),
                    result,
                    EvolutionConsumePolicy.RetainRequiredPassives);
                AddReference(references, source);
                AddReference(references, result);
                AddReference(references, request.PassiveReference);
            }
            else if (asset is SynergyAuthoring synergy)
            {
                synergy.Configure(
                    new[]
                    {
                        new BuildConditionAuthoringData
                        {
                            type = BuildConditionType.MapHasTag,
                            tag = "map.finite"
                        }
                    },
                    new[]
                    {
                        new SynergyOutputAuthoringData
                        {
                            type = SynergyOutputType.AddModifier,
                            modifier = Modifier("base.stat.damage", 0.1f)
                        }
                    });
            }
            else if (asset is MapAuthoring mapAuthoring)
            {
                var mapEncounter = request.EncounterReference ?? FindFirst<EncounterScheduleAuthoring>(value => value != null);
                if (mapEncounter == null) throw new InvalidOperationException("Create or select an Encounter before a Map.");
                mapAuthoring.ConfigureM5(
                    "base.map.finite",
                    "maps/" + asset.ContentIdText,
                    MapBoundsMode.Finite,
                    new Vector2(-24f, -14f),
                    new Vector2(24f, 14f),
                    16f,
                    2,
                    mapEncounter,
                    "placeholder.visual." + asset.ContentIdText,
                    Array.Empty<MapObstacleAuthoringData>(),
                    Array.Empty<MapAnchorAuthoringData>());
                AddReference(references, mapEncounter);
            }
            else if (asset is EncounterScheduleAuthoring encounterAuthoring)
            {
                var encounterEnemy = request.EnemyReference ?? FindFirst<EnemyAuthoring>(value => value.M5RuntimeEnabled);
                if (encounterEnemy == null) throw new InvalidOperationException("Create or select an M5 Enemy before an Encounter.");
                encounterAuthoring.Configure(
                    32,
                    10f,
                    16f,
                    new[]
                    {
                        new EncounterPhaseAuthoringData
                        {
                            startTimeSeconds = 0f,
                            endTimeSeconds = 60f,
                            budgetPerSecondStart = 1f,
                            budgetPerSecondEnd = 2f,
                            spawnIntervalStart = 1f,
                            spawnIntervalEnd = 0.5f,
                            maximumConcurrentEnemies = 32,
                            spawnPattern = SpawnPattern.Ring,
                            enemies = new[]
                            {
                                new EncounterEnemyEntryAuthoringData
                                {
                                    enemy = encounterEnemy,
                                    weight = 1f,
                                    budgetCost = 1f,
                                    minimumGroupSize = 1,
                                    maximumGroupSize = 2
                                }
                            },
                            bosses = Array.Empty<EncounterBossRuleAuthoringData>()
                        }
                    });
                AddReference(references, encounterEnemy);
            }
            else
            {
                throw new InvalidOperationException("Unsupported authoring type " + asset.GetType().Name + ".");
            }

            return references;
        }

        private static void AddDefinitionAndDependencies(
            ContentPackAuthoring pack,
            ContentAuthoringBase asset,
            IReadOnlyList<ContentAuthoringBase> references)
        {
            var definitions = new List<ContentAuthoringBase>(pack.Definitions.Count + 1);
            for (var index = 0; index < pack.Definitions.Count; index++)
                if (pack.Definitions[index] != asset) definitions.Add(pack.Definitions[index]);
            definitions.Add(asset);
            var dependencies = new List<ContentPackDependencyAuthoring>(pack.Dependencies.Count);
            for (var index = 0; index < pack.Dependencies.Count; index++)
                dependencies.Add(pack.Dependencies[index]);
            for (var referenceIndex = 0; referenceIndex < references.Count; referenceIndex++)
            {
                var owner = ContentEditorCatalog.FindOwningPack(references[referenceIndex]);
                if (owner == null || owner == pack || HasDependency(dependencies, owner.PackIdText)) continue;
                dependencies.Add(new ContentPackDependencyAuthoring
                {
                    packId = owner.PackIdText,
                    minimumVersion = owner.VersionText,
                    maximumVersion = owner.VersionText
                });
            }

            pack.Configure(
                pack.PackIdText,
                pack.VersionText,
                pack.SchemaVersion,
                pack.MinimumGameVersionText,
                pack.MaximumGameVersionText,
                dependencies.ToArray(),
                pack.CatalogAddress,
                pack.AssetLabel,
                pack.Official,
                definitions.ToArray());
        }

        private static ContentAuthoringBase LoadOrCreateDefinition(
            ContentCreationKind kind,
            string path,
            bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ContentAuthoringBase>(path);
            if (existing != null)
            {
                if (!overwrite) throw new InvalidOperationException("Content already exists at " + path + ".");
                if (!MatchesKind(existing, kind))
                    throw new InvalidOperationException(path + " contains a different content type.");
                return existing;
            }

            ContentAuthoringBase asset;
            switch (kind)
            {
                case ContentCreationKind.Character: asset = ScriptableObject.CreateInstance<CharacterAuthoring>(); break;
                case ContentCreationKind.Skill: asset = ScriptableObject.CreateInstance<SkillAuthoring>(); break;
                case ContentCreationKind.Passive: asset = ScriptableObject.CreateInstance<PassiveAuthoring>(); break;
                case ContentCreationKind.Trait: asset = ScriptableObject.CreateInstance<TraitAuthoring>(); break;
                case ContentCreationKind.Enemy: asset = ScriptableObject.CreateInstance<EnemyAuthoring>(); break;
                case ContentCreationKind.Status: asset = ScriptableObject.CreateInstance<StatusEffectAuthoring>(); break;
                case ContentCreationKind.Evolution: asset = ScriptableObject.CreateInstance<EvolutionAuthoring>(); break;
                case ContentCreationKind.Synergy: asset = ScriptableObject.CreateInstance<SynergyAuthoring>(); break;
                case ContentCreationKind.Map: asset = ScriptableObject.CreateInstance<MapAuthoring>(); break;
                case ContentCreationKind.Encounter: asset = ScriptableObject.CreateInstance<EncounterScheduleAuthoring>(); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static bool MatchesKind(ContentAuthoringBase asset, ContentCreationKind kind)
        {
            return kind == ContentCreationKind.Character && asset is CharacterAuthoring ||
                   kind == ContentCreationKind.Skill && asset is SkillAuthoring ||
                   kind == ContentCreationKind.Passive && asset is PassiveAuthoring ||
                   kind == ContentCreationKind.Trait && asset is TraitAuthoring ||
                   kind == ContentCreationKind.Enemy && asset is EnemyAuthoring ||
                   kind == ContentCreationKind.Status && asset is StatusEffectAuthoring ||
                   kind == ContentCreationKind.Evolution && asset is EvolutionAuthoring ||
                   kind == ContentCreationKind.Synergy && asset is SynergyAuthoring ||
                   kind == ContentCreationKind.Map && asset is MapAuthoring ||
                   kind == ContentCreationKind.Encounter && asset is EncounterScheduleAuthoring;
        }

        private static string BakeAndLabel(ContentPackAuthoring pack, string packPath)
        {
            var baked = ContentBakeUtility.Bake(pack);
            if (!baked.IsSuccess) throw new UnityException(baked.Error.ToString());
            var bakedPath = ContentBakeUtility.WriteCatalog(packPath, baked.Value);
            M9AddressableUtility.Configure(bakedPath, pack.CatalogAddress, pack.AssetLabel);
            return bakedPath;
        }

        private static void EnsureDefinitionIdIsUnique(string id, ContentAuthoringBase allowed)
        {
            var guids = AssetDatabase.FindAssets("t:ContentPackAuthoring");
            for (var packIndex = 0; packIndex < guids.Length; packIndex++)
            {
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(
                    AssetDatabase.GUIDToAssetPath(guids[packIndex]));
                if (pack == null) continue;
                for (var index = 0; index < pack.Definitions.Count; index++)
                {
                    var definition = pack.Definitions[index];
                    if (definition != allowed && definition != null &&
                        string.Equals(definition.ContentIdText, id, StringComparison.Ordinal))
                        throw new InvalidOperationException("ContentId already exists: " + id + ".");
                }
            }
        }

        private static void EnsurePackIdIsUnique(string id, ContentPackAuthoring allowed)
        {
            var guids = AssetDatabase.FindAssets("t:ContentPackAuthoring");
            for (var index = 0; index < guids.Length; index++)
            {
                var pack = AssetDatabase.LoadAssetAtPath<ContentPackAuthoring>(
                    AssetDatabase.GUIDToAssetPath(guids[index]));
                if (pack != null && pack != allowed &&
                    string.Equals(pack.PackIdText, id, StringComparison.Ordinal))
                    throw new InvalidOperationException("PackId already exists: " + id + ".");
            }
        }

        private static string WriteTestTemplate(
            string packFolder,
            string id,
            string kind,
            string assetPath,
            string[] localizationKeys)
        {
            var folder = packFolder + "/Tests";
            EnsureAssetFolder(folder);
            var path = folder + "/" + id.Replace('.', '_') + ".content-test.json";
            var template = new ContentTestTemplateDto
            {
                schemaVersion = 1,
                contentId = id,
                contentKind = kind,
                authoringAsset = assetPath,
                expectedLocalizationKeys = localizationKeys ?? Array.Empty<string>(),
                checks = new[]
                {
                    "bakes",
                    "localization-keys-resolve",
                    "addressables-labels-present",
                    "project-validation-pass"
                }
            };
            WriteAssetText(path, JsonUtility.ToJson(template, true) + "\n");
            return path;
        }

        private static string WriteSourceRecord(ContentPackAuthoring pack, string folder)
        {
            var ids = new string[pack.Definitions.Count];
            for (var index = 0; index < ids.Length; index++)
                ids[index] = pack.Definitions[index] == null
                    ? string.Empty
                    : pack.Definitions[index].ContentIdText;
            Array.Sort(ids, StringComparer.Ordinal);
            var path = folder + "/provenance.placeholder.json";
            var record = new PlaceholderSourceRecordDto
            {
                schemaVersion = 1,
                packId = pack.PackIdText,
                sourceCategory = "programmatic-placeholder",
                generatedBy = "M9 Content Creation Wizard",
                status = "development-only",
                commercialUseReviewed = false,
                contentIds = ids
            };
            WriteAssetText(path, JsonUtility.ToJson(record, true) + "\n");
            return path;
        }

        private static void WriteAssetText(string assetPath, string content)
        {
            File.WriteAllText(Path.GetFullPath(assetPath), content, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureAssetFolder(string path)
        {
            var normalized = NormalizeAssetFolder(path);
            if (AssetDatabase.IsValidFolder(normalized)) return;
            var segments = normalized.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException("Wizard folders must be inside Assets.");
            var current = "Assets";
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private static string NormalizeAssetFolder(string path)
        {
            var value = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (!value.StartsWith("Assets/", StringComparison.Ordinal) &&
                !string.Equals(value, "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException("Folder must be a project-relative Assets path.");
            return value;
        }

        private static string SanitizeIdentifier(string value)
        {
            var output = new StringBuilder();
            var pendingSeparator = false;
            var source = value ?? string.Empty;
            for (var index = 0; index < source.Length; index++)
            {
                var character = char.ToLowerInvariant(source[index]);
                if (character >= 'a' && character <= 'z' || character >= '0' && character <= '9')
                {
                    if (pendingSeparator && output.Length > 0) output.Append('_');
                    output.Append(character);
                    pendingSeparator = false;
                }
                else pendingSeparator = output.Length > 0;
            }

            return output.ToString().Trim('_');
        }

        private static string PascalCase(string slug)
        {
            var output = new StringBuilder(slug.Length);
            var uppercase = true;
            for (var index = 0; index < slug.Length; index++)
            {
                if (slug[index] == '_')
                {
                    uppercase = true;
                    continue;
                }

                output.Append(uppercase ? char.ToUpperInvariant(slug[index]) : slug[index]);
                uppercase = false;
            }

            return output.ToString();
        }

        private static string KindToken(ContentCreationKind kind)
        {
            switch (kind)
            {
                case ContentCreationKind.Character: return "character";
                case ContentCreationKind.Skill: return "skill";
                case ContentCreationKind.Passive: return "passive";
                case ContentCreationKind.Trait: return "trait";
                case ContentCreationKind.Enemy: return "enemy";
                case ContentCreationKind.Status: return "status";
                case ContentCreationKind.Evolution: return "evolution";
                case ContentCreationKind.Synergy: return "synergy";
                case ContentCreationKind.Map: return "map";
                case ContentCreationKind.Encounter: return "encounter";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static string KindFolder(ContentCreationKind kind)
        {
            switch (kind)
            {
                case ContentCreationKind.Character: return "Characters";
                case ContentCreationKind.Skill: return "Skills";
                case ContentCreationKind.Passive: return "Passives";
                case ContentCreationKind.Trait: return "Traits";
                case ContentCreationKind.Enemy: return "Enemies";
                case ContentCreationKind.Status: return "Statuses";
                case ContentCreationKind.Evolution: return "Evolutions";
                case ContentCreationKind.Synergy: return "Synergies";
                case ContentCreationKind.Map: return "Maps";
                case ContentCreationKind.Encounter: return "Encounters";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static string[] BuildTags(ContentCreationKind kind)
        {
            var tags = new List<string>
            {
                "content.placeholder",
                "content." + KindToken(kind)
            };
            if (kind == ContentCreationKind.Character) tags.Add("actor.player");
            if (kind == ContentCreationKind.Enemy) tags.Add("actor.enemy");
            if (kind == ContentCreationKind.Map) tags.Add("map.finite");
            return tags.ToArray();
        }

        private static SkillModuleAuthoringData Module(
            string id,
            float value0 = 0f,
            int int0 = 0)
        {
            return new SkillModuleAuthoringData { moduleId = id, value0 = value0, int0 = int0 };
        }

        private static BuildModifierAuthoringData Modifier(string statId, float value)
        {
            return new BuildModifierAuthoringData
            {
                statId = statId,
                operation = ModifierOperation.AddFlat,
                value = value
            };
        }

        private static PassiveLevelModifierAuthoringData PassiveModifier(int level, float value)
        {
            return new PassiveLevelModifierAuthoringData
            {
                level = level,
                modifier = Modifier("base.stat.damage", value)
            };
        }

        private static void AddReference(List<ContentAuthoringBase> output, ContentAuthoringBase value)
        {
            if (value != null && !output.Contains(value)) output.Add(value);
        }

        private static bool HasDependency(
            IReadOnlyList<ContentPackDependencyAuthoring> dependencies,
            string id)
        {
            for (var index = 0; index < dependencies.Count; index++)
                if (string.Equals(dependencies[index].packId, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static T FindFirst<T>(Predicate<T> predicate) where T : ContentAuthoringBase
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var paths = new string[guids.Length];
            for (var index = 0; index < guids.Length; index++)
                paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            Array.Sort(paths, StringComparer.Ordinal);
            for (var index = 0; index < paths.Length; index++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(paths[index]);
                if (asset != null && (predicate == null || predicate(asset))) return asset;
            }

            return null;
        }

        private static ContentAuthoringBase[] CopyDefinitions(IReadOnlyList<ContentAuthoringBase> source)
        {
            var output = new ContentAuthoringBase[source == null ? 0 : source.Count];
            for (var index = 0; index < output.Length; index++) output[index] = source[index];
            return output;
        }

        private static ContentPackDependencyAuthoring[] CopyDependencies(
            IReadOnlyList<ContentPackDependencyAuthoring> source)
        {
            var output = new ContentPackDependencyAuthoring[source == null ? 0 : source.Count];
            for (var index = 0; index < output.Length; index++) output[index] = source[index];
            return output;
        }

        [Serializable]
        private sealed class ContentTestTemplateDto
        {
            public int schemaVersion;
            public string contentId;
            public string contentKind;
            public string authoringAsset;
            public string[] expectedLocalizationKeys;
            public string[] checks;
        }

        [Serializable]
        private sealed class PlaceholderSourceRecordDto
        {
            public int schemaVersion;
            public string packId;
            public string sourceCategory;
            public string generatedBy;
            public string status;
            public bool commercialUseReviewed;
            public string[] contentIds;
        }
    }

    internal static class M9AddressableUtility
    {
        internal static void Configure(string assetPath, string address, string packLabel)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null || settings.DefaultGroup == null)
                throw new InvalidOperationException("Addressables settings are unavailable.");
            settings.AddLabel(PlaceholderAssetGenerator.PlaceholderLabel);
            settings.AddLabel(PlaceholderAssetGenerator.DevelopmentOnlyLabel);
            settings.AddLabel(packLabel);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, false);
            entry.address = address;
            entry.SetLabel(PlaceholderAssetGenerator.PlaceholderLabel, true, false, false);
            entry.SetLabel(PlaceholderAssetGenerator.DevelopmentOnlyLabel, true, false, false);
            entry.SetLabel(packLabel, true, false, false);
            EditorUtility.SetDirty(settings);
        }
    }

    internal static class M9LocalizationUtility
    {
        internal static void EnsureContentEntries(
            string nameKey,
            string descriptionKey,
            string displayName)
        {
            EnsureContentEntries(
                nameKey,
                descriptionKey,
                "[Placeholder] " + displayName,
                "[Placeholder] " + displayName + " description",
                "[占位] " + displayName,
                "[占位] " + displayName + " 描述");
        }

        internal static void EnsureContentEntries(
            string nameKey,
            string descriptionKey,
            string englishName,
            string englishDescription,
            string chineseName,
            string chineseDescription)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            var englishLocale = FindLocale("en");
            var chineseLocale = FindLocale("zh-Hans");
            var english = englishLocale == null || collection == null
                ? null
                : collection.GetTable(englishLocale.Identifier) as StringTable;
            var chinese = chineseLocale == null || collection == null
                ? null
                : collection.GetTable(chineseLocale.Identifier) as StringTable;
            if (english == null || chinese == null)
                throw new InvalidOperationException("Run the M8 Localization setup before using the M9 wizard.");
            Set(english, nameKey, englishName);
            Set(english, descriptionKey, englishDescription);
            Set(chinese, nameKey, chineseName);
            Set(chinese, descriptionKey, chineseDescription);
            EditorUtility.SetDirty(english);
            EditorUtility.SetDirty(chinese);
            EditorUtility.SetDirty(collection.SharedData);
        }

        private static UnityEngine.Localization.Locale FindLocale(string code)
        {
            var locales = LocalizationEditorSettings.GetLocales();
            for (var index = 0; index < locales.Count; index++)
            {
                if (!(locales[index] is PseudoLocale) &&
                    string.Equals(locales[index].Identifier.Code, code, StringComparison.OrdinalIgnoreCase))
                    return locales[index];
            }

            return null;
        }

        private static void Set(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
            entry.Value = value;
        }
    }
}
