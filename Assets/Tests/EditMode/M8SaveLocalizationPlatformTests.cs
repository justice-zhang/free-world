using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Infrastructure;
using Game.Platform.Abstractions;
using Game.Platform.Null;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Tables;

namespace Game.Tests.EditMode
{
    public sealed class M8SaveLocalizationPlatformTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "AzureSwordM8Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ThreeDocumentsRoundTripStableIdsWithoutRuntimeIndexOrUnityObjects()
        {
            var codec = new UnityJsonSaveCodec();
            var pack = new SavePackVersion(Id("test.pack.m1"), new ContentVersion(0, 1, 0));
            var settings = new SettingsSaveData("zh-Hans", 0.2f, 0.7f, false, 0.4f, false,
                AutoAimStrategy.MovementDirection,
                new[] { new SavedBindingOverride("UI/Submit", 0, "<Keyboard>/space") });
            var profile = new ProfileSaveData("profile-a", new[] { pack }, new[] { Id("test.skill.pulse") },
                new[] { new SavedContentLevel(Id("test.skill.pulse"), 2) },
                new[] { new SavedCounter("coins", 9) }, new[] { new SavedCounter("runs_completed", 3) }, "2026-07-26T00:00:00Z");
            var recovery = new RunRecoverySaveData(77, 42, Id("test.character.runner"), Id("test.map.finite_arena"),
                new[] { pack }, new[] { new SavedContentLevel(Id("test.skill.single_projectile"), 1) }, "2026-07-26T00:00:00Z");

            var settingsBytes = codec.Encode(settings);
            var profileBytes = codec.Encode(profile);
            var recoveryBytes = codec.Encode(recovery);
            Assert.That(settingsBytes.IsSuccess && profileBytes.IsSuccess && recoveryBytes.IsSuccess, Is.True);
            Assert.That(Encoding.UTF8.GetString(settingsBytes.Data), Does.Not.Contain("RuntimeContentIndex"));
            Assert.That(Encoding.UTF8.GetString(profileBytes.Data), Does.Not.Contain("RuntimeContentIndex"));
            Assert.That(Encoding.UTF8.GetString(recoveryBytes.Data), Does.Not.Contain("RuntimeContentIndex"));
            Assert.That(codec.DecodeSettings(settingsBytes.Data).Value.LocaleCode, Is.EqualTo("zh-Hans"));
            Assert.That(codec.DecodeProfile(profileBytes.Data).Value.UnlockedContentIds[0], Is.EqualTo(Id("test.skill.pulse")));
            Assert.That(codec.DecodeRunRecovery(recoveryBytes.Data).Value.RunSeed, Is.EqualTo(77));

            var saveTypes = new[] { typeof(SettingsSaveData), typeof(ProfileSaveData), typeof(RunRecoverySaveData) };
            for (var index = 0; index < saveTypes.Length; index++)
            {
                Assert.That(saveTypes[index].Assembly.GetName().Name, Is.EqualTo("Game.Application"));
                Assert.That(saveTypes[index].GetFields(), Is.Empty);
            }
        }

        [Test]
        public async Task CancellationAfterTemporaryFlushPreservesPreviousPrimary()
        {
            var first = Encoding.UTF8.GetBytes("first-version");
            var second = Encoding.UTF8.GetBytes("second-version");
            var normal = new LocalFileSaveStorage(directory);
            Assert.That((await normal.WriteAtomicAsync(SaveSlots.Settings, first, default)).IsSuccess, Is.True);
            var cancellation = new CancellationTokenSource();
            var interrupted = new LocalFileSaveStorage(directory, new CancellingObserver(cancellation));

            var result = await interrupted.WriteAtomicAsync(SaveSlots.Settings, second, cancellation.Token);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Diagnostic.Code, Is.EqualTo(SaveFailureCode.Cancelled));
            CollectionAssert.AreEqual(first, File.ReadAllBytes(Path.Combine(directory, SaveSlots.Settings)));
            Assert.That(File.Exists(Path.Combine(directory, SaveSlots.Settings + ".tmp")), Is.False);
        }

        [Test]
        public async Task CorruptPrimaryRestoresValidBackupAndReportsChecksumFailureWithoutOne()
        {
            var codec = new UnityJsonSaveCodec();
            var storage = new LocalFileSaveStorage(directory);
            var coordinator = new SaveCoordinator(storage, codec, new ContentRegistry());
            var first = new SettingsSaveData("en", 0.15f, 1f, true, 1f, true, AutoAimStrategy.Nearest);
            var second = new SettingsSaveData("zh-Hans", 0.25f, 0.5f, false, 0.5f, false, AutoAimStrategy.Disabled);
            Assert.That((await coordinator.SaveSettingsAsync(first)).IsSuccess, Is.True);
            Assert.That((await coordinator.SaveSettingsAsync(second)).IsSuccess, Is.True);
            var primaryPath = Path.Combine(directory, SaveSlots.Settings);
            File.WriteAllBytes(primaryPath, CorruptChecksum(File.ReadAllBytes(primaryPath)));

            var recovered = await coordinator.LoadSettingsAsync();
            Assert.That(recovered.IsSuccess, Is.True);
            Assert.That(recovered.Source, Is.EqualTo(SaveReadSource.Backup));
            Assert.That(recovered.Value.LocaleCode, Is.EqualTo("en"));
            Assert.That(recovered.Diagnostics[0].MessageKey, Is.EqualTo("save.warning.recovered_backup"));

            File.Delete(Path.Combine(directory, SaveSlots.Settings + ".bak"));
            var failed = await coordinator.LoadSettingsAsync();
            Assert.That(failed.IsSuccess, Is.False);
            Assert.That(failed.Failure.Code, Is.EqualTo(SaveFailureCode.ChecksumMismatch));
        }

        [Test]
        public void VersionOneSettingsSampleMigratesToVersionThree()
        {
            const string legacy = "{\"schemaVersion\":1,\"localeCode\":\"zh-Hans\",\"stickDeadzone\":0.3}";
            var codec = new UnityJsonSaveCodec();
            var envelope = codec.EncodeRawPayload(SaveDocumentKind.Settings, 1, legacy);

            var result = codec.DecodeSettings(envelope);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.SchemaVersion, Is.EqualTo(3));
            Assert.That(result.Value.LocaleCode, Is.EqualTo("zh-Hans"));
            Assert.That(result.Value.StickDeadzone, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(result.Value.DamageNumbersEnabled, Is.True);
            Assert.That(result.Value.FontScale, Is.EqualTo(1f));
            Assert.That(result.Value.SubtitlesEnabled, Is.True);
        }

        [Test]
        public async Task MissingProfileContentWarnsButTransactionIdsDoNotAndRecoveryFailsCleanly()
        {
            var storage = new LocalFileSaveStorage(directory);
            var coordinator = new SaveCoordinator(storage, new UnityJsonSaveCodec(), CreateRuntimeRegistry());
            var missing = Id("missing.skill.removed");
            var profile = new ProfileSaveData("profile", Array.Empty<SavePackVersion>(), new[] { missing },
                Array.Empty<SavedContentLevel>(), Array.Empty<SavedCounter>(), Array.Empty<SavedCounter>(), "now",
                activeMetaLoadoutIds: new[] { missing },
                firstClearMapIds: new[] { missing },
                claimedUniqueRewardIds: new[] { missing },
                completedStoryIds: new[] { missing },
                collectedCollectibleIds: new[] { missing },
                committedTransactionIds: new[] { Id("missing.transaction.not_content") });
            Assert.That((await coordinator.SaveProfileAsync(profile)).IsSuccess, Is.True);

            var profileLoad = await coordinator.LoadProfileAsync();
            Assert.That(profileLoad.IsSuccess, Is.True);
            Assert.That(profileLoad.Value.UnlockedContentIds[0], Is.EqualTo(missing), "original ID must remain available for diagnostics");
            Assert.That(profileLoad.Diagnostics[0].MessageKey, Is.EqualTo("save.warning.missing_unlock"));
            Assert.That(profileLoad.Diagnostics.Count, Is.EqualTo(6));

            var recovery = new RunRecoverySaveData(1, 0, Id("test.character.runner"), Id("test.map.finite_arena"),
                Array.Empty<SavePackVersion>(), new[] { new SavedContentLevel(missing, 1) }, "now");
            Assert.That((await coordinator.SaveRunRecoveryAsync(recovery)).IsSuccess, Is.True);
            var recoveryLoad = await coordinator.LoadRunRecoveryAsync();
            Assert.That(recoveryLoad.IsSuccess, Is.False);
            Assert.That(recoveryLoad.Failure.Code, Is.EqualTo(SaveFailureCode.MissingContent));
            Assert.That(recoveryLoad.Failure.ContentId, Is.EqualTo(missing));
        }

        [Test]
        public void UnityLocalizationLoadsEnglishChineseAndPseudoWithoutRawUiKeys()
        {
            var service = new UnityLocalizationService();
            Assert.That(service.SelectLocale("en"), Is.True);
            Assert.That(service.Resolve("ui.main_menu.start"), Is.EqualTo("Start Test Run"));
            Assert.That(service.SelectLocale("zh-Hans"), Is.True);
            Assert.That(service.Resolve("ui.main_menu.start"), Is.EqualTo("开始测试局"));
            Assert.That(service.SelectLocale("pseudo"), Is.True);
            var pseudo = service.Resolve("ui.main_menu.start");
            Assert.That(pseudo, Is.Not.EqualTo("Start Test Run"));
            Assert.That(pseudo.Length, Is.GreaterThan("Start Test Run".Length));

            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            var english = collection.GetTable("en") as StringTable;
            var chinese = collection.GetTable("zh-Hans") as StringTable;
            Assert.That(english.GetEntry("ui.settings.language"), Is.Not.Null);
            Assert.That(chinese.GetEntry("ui.settings.language"), Is.Not.Null);
            Assert.That(LocalizationEditorSettings.GetPseudoLocales(), Has.Some.InstanceOf<PseudoLocale>());
        }

        [Test]
        public void CloudConflictPolicyHandlesLocalNewerRemoteNewerAndDivergence()
        {
            var policy = new ConservativeCloudConflictStrategy();
            var baseline = "base";
            var localNewer = new CloudSyncState(Revision("local"), Revision(baseline), baseline);
            var remoteNewer = new CloudSyncState(Revision(baseline), Revision("remote"), baseline);
            var diverged = new CloudSyncState(Revision("local"), Revision("remote"), baseline);

            Assert.That(policy.Resolve(localNewer).Decision, Is.EqualTo(CloudConflictDecision.UploadLocal));
            Assert.That(policy.Resolve(remoteNewer).Decision, Is.EqualTo(CloudConflictDecision.DownloadRemote));
            Assert.That(policy.Resolve(diverged).Decision, Is.EqualTo(CloudConflictDecision.RequireUserChoice));
        }

        [Test]
        public void NullPlatformCompletesEveryBoundaryWithoutSteam()
        {
            var platform = new NullPlatformFacade();
            Assert.That(platform.IsAvailable, Is.False);
            Assert.That(platform.Identity.Current.IsSignedIn, Is.False);
            Assert.That(platform.Achievements.UnlockAsync(Id("test.achievement.one")).AsTask().Result.Status, Is.EqualTo(PlatformOperationStatus.Unavailable));
            Assert.That(platform.Stats.AddAsync(Id("test.stat.runs"), 1).AsTask().Result.Status, Is.EqualTo(PlatformOperationStatus.Unavailable));
            Assert.That(platform.Cloud.GetRemoteRevisionAsync(SaveSlots.Profile).AsTask().Result.Exists, Is.False);
            Assert.That(platform.RichPresence.SetAsync(Id("test.presence.menu")).AsTask().Result.Status, Is.EqualTo(PlatformOperationStatus.Unavailable));
        }

        [Test]
        public void SimulationAssemblyHasNoPlatformReference()
        {
            var asmdef = File.ReadAllText("Assets/Game/Simulation/Game.Simulation.asmdef");
            Assert.That(asmdef, Does.Not.Contain("Game.Platform"));
            var files = Directory.GetFiles("Assets/Game/Simulation", "*.cs", SearchOption.AllDirectories);
            for (var index = 0; index < files.Length; index++)
                Assert.That(File.ReadAllText(files[index]), Does.Not.Contain("Game.Platform"), files[index]);
        }

        private static ContentRegistry CreateRuntimeRegistry()
        {
            var paths = new[]
            {
                "Assets/GameAssets/Placeholder/TestContent/TestM1ContentPack.baked.json",
                "Assets/GameAssets/Placeholder/TestSkillContent/TestM4SkillContentPack.baked.json",
                "Assets/GameAssets/Placeholder/TestM5Content/TestM5ContentPack.baked.json",
                "Assets/GameAssets/Placeholder/TestBuildContent/TestM6BuildContentPack.baked.json"
            };
            var catalogs = new BakedContentCatalog[paths.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[index]);
                var parsed = JsonUtility.FromJson<BakedContentCatalogDto>(asset.text).ToCatalog();
                Assert.That(parsed.IsSuccess, Is.True, paths[index]);
                catalogs[index] = parsed.Value;
            }
            var registry = new ContentRegistry();
            var loaded = registry.Load(catalogs, new ContentVersion(0, 1, 0));
            Assert.That(loaded.IsSuccess, Is.True);
            return registry;
        }

        private static ContentId Id(string value) => ContentId.Create(value).Value;
        private static CloudFileRevision Revision(string checksum) => new CloudFileRevision(true, checksum, "2026-07-26T00:00:00Z", "device", 1);

        private static byte[] CorruptChecksum(byte[] envelope)
        {
            var json = Encoding.UTF8.GetString(envelope);
            const string marker = "\"checksumSha256\": \"";
            var checksumStart = json.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(checksumStart, Is.GreaterThanOrEqualTo(0));
            checksumStart += marker.Length;
            var replacement = json[checksumStart] == '0' ? '1' : '0';
            var chars = json.ToCharArray();
            chars[checksumStart] = replacement;
            return Encoding.UTF8.GetBytes(chars);
        }

        private sealed class CancellingObserver : IAtomicSaveWriteObserver
        {
            private readonly CancellationTokenSource cancellation;
            public CancellingObserver(CancellationTokenSource source) { cancellation = source; }
            public void OnStage(AtomicSaveWriteStage stage)
            {
                if (stage == AtomicSaveWriteStage.TemporaryFileFlushed) cancellation.Cancel();
            }
        }
    }
}
