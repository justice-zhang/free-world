using System;
using System.Collections;
using System.IO;
using Game.Application;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class M8SaveLocalizationFlowPlayModeTests
    {
        private string saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyBootstrapInstances();
            yield return null;
            saveRoot = Path.Combine(Path.GetTempPath(), "AzureSwordM8PlayMode", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("AZURESWORD_SAVE_ROOT", saveRoot);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyBootstrapInstances();
            yield return null;
            Environment.SetEnvironmentVariable("AZURESWORD_SAVE_ROOT", null);
            if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        }

        [UnityTest]
        public IEnumerator NullPlatformFullFlowSavesSettingsProfileAndRecoveryLocally()
        {
            var operation = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
            yield return null;
            var bootstrap = Object.FindFirstObjectByType<GameBootstrapper>();
            var host = Object.FindFirstObjectByType<M7RuntimeHost>();

            Assert.That(bootstrap.PlatformFacade.IsAvailable, Is.False);
            Assert.That(host.Ui.RenderedText, Does.Contain("Free World Framework"));
            Assert.That(host.Ui.RenderedText, Does.Not.Contain("ui.main_menu.title"));
            Assert.That(host.Localization.SelectLocale("zh-Hans"), Is.True);
            host.Flow.ShowCharacterSelect();
            host.TickRuntime(0d);
            Assert.That(host.Ui.RenderedText, Does.Contain("选择角色"));
            Assert.That(host.Ui.SupportsCharacter('中'), Is.True);
            Assert.That(host.Localization.SelectLocale("pseudo"), Is.True);
            host.Flow.ReturnToMainMenu();
            host.TickRuntime(0d);
            Assert.That(host.Ui.RenderedText, Does.Not.Contain("ui.main_menu.title"));
            Assert.That(host.Ui.RenderedText.Length, Is.GreaterThan("Free World Framework".Length));
            Assert.That(host.Localization.SelectLocale("en"), Is.True);
            host.Flow.OpenSettings();
            host.TickRuntime(0d);
            host.Flow.Settings.SetStickDeadzone(0.3f);
            host.Flow.CloseSettings();
            host.TickRuntime(0d);
            Assert.That(File.Exists(Path.Combine(saveRoot, SaveSlots.Settings)), Is.True);

            host.Flow.ShowCharacterSelect();
            host.Flow.ShowMapSelect();
            host.Flow.BeginRun();
            host.TickRuntime(0d);
            Assert.That(File.Exists(Path.Combine(saveRoot, SaveSlots.RunRecovery)), Is.True);
            Assert.That(host.Flow.EndRun(RunEndReason.Completed), Is.True);
            Assert.That(File.Exists(Path.Combine(saveRoot, SaveSlots.Profile)), Is.True);
            Assert.That(File.Exists(Path.Combine(saveRoot, SaveSlots.RunRecovery)), Is.False);
            Assert.That(bootstrap.Persistence.LastPlatformOperation.Status, Is.EqualTo(Game.Platform.Abstractions.PlatformOperationStatus.Unavailable));
        }

        private static void DestroyBootstrapInstances()
        {
            var all = Object.FindObjectsByType<GameBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < all.Length; index++) Object.Destroy(all[index].gameObject);
        }
    }
}
