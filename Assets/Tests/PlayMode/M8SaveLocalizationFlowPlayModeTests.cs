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
            var host = Object.FindFirstObjectByType<QinglanDemoRuntimeHost>();

            Assert.That(bootstrap.PlatformFacade.IsAvailable, Is.False);
            Assert.That(host.Ui.RenderedPageText, Does.Contain("Sword Rises in Qinglan"));
            Assert.That(host.Ui.RenderedPageText, Does.Not.Contain("ui.qinglan.title.name"));
            Assert.That(host.Localization.SelectLocale("zh-Hans"), Is.True);
            host.TickRuntime(0d);
            Assert.That(host.Ui.RenderedPageText, Does.Contain("剑起青岚"));
            Assert.That(host.Ui.SupportsCharacter('中'), Is.True);
            Assert.That(host.Localization.SelectLocale("pseudo"), Is.True);
            host.TickRuntime(0d);
            Assert.That(host.Ui.RenderedPageText, Does.Not.Contain("ui.qinglan.title.name"));
            Assert.That(host.Ui.RenderedPageText.Length, Is.GreaterThan("Sword Rises in Qinglan".Length));
            Assert.That(host.Localization.SelectLocale("en"), Is.True);
            host.Flow.Execute(Game.UI.QinglanUiCommand.OpenSettings, "settings", 0);
            host.TickRuntime(0d);
            host.Flow.Settings.SetStickDeadzone(0.3f);
            host.Flow.Cancel();
            host.TickRuntime(0d);
            Assert.That(File.Exists(Path.Combine(saveRoot, SaveSlots.Settings)), Is.True);

            host.Flow.Execute(Game.UI.QinglanUiCommand.Start, "start", 0);
            host.Flow.Execute(Game.UI.QinglanUiCommand.Continue, "character", 0);
            host.Flow.Execute(Game.UI.QinglanUiCommand.OpenLoadout, "map", 0);
            host.Flow.Execute(Game.UI.QinglanUiCommand.BeginRun, "begin", 0);
            host.TickRuntime(0d);
            Assert.That(File.Exists(Path.Combine(saveRoot, SaveSlots.RunRecovery)), Is.True);
            Assert.That(host.Flow.DebugCompleteRun(), Is.True);
            host.TickRuntime(0d);
            for (var index = 0; index < 10 && host.Flow.Stage != DemoFlowStage.Result; index++)
            {
                host.TickRuntime(0d);
                yield return null;
            }
            for (var index = 0; index < 20 && host.Flow.LastCommit.IsSuccess == false; index++)
            {
                host.TickRuntime(0d);
                yield return null;
            }
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
