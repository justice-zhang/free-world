using System.Collections;
using Game.Application;
using Game.Core;
using Game.Infrastructure;
using Game.Presentation;
using Game.UI;
using Game.Platform.Null;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class BootstrapPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var existing = Object.FindObjectsByType<GameBootstrapper>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < existing.Length; index++)
            {
                Object.Destroy(existing[index].gameObject);
            }

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var existing = Object.FindObjectsByType<GameBootstrapper>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < existing.Length; index++)
            {
                Object.Destroy(existing[index].gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BootstrapSceneStartsAndEntersEmptyMainMenu()
        {
            yield return LoadBootstrapScene();
            var bootstrapper = GetOnlyBootstrapper();

            Assert.That(bootstrapper.Application, Is.Not.Null);
            Assert.That(bootstrapper.Application.IsInitialized, Is.True);
            Assert.That(bootstrapper.CurrentState, Is.EqualTo(GameState.MainMenu));
        }

        [UnityTest]
        public IEnumerator BootstrapLoadsTestPackAndReportsEntryCount()
        {
            yield return LoadBootstrapScene();
            var bootstrapper = GetOnlyBootstrapper();
            var skillId = ContentId.Create("test.skill.pulse").Value;

            Assert.That(bootstrapper.ContentSummary.PackCount, Is.EqualTo(5));
            Assert.That(bootstrapper.ContentSummary.DefinitionCount, Is.EqualTo(220));
            Assert.That(
                bootstrapper.Application.ContentRegistry.TryGet(skillId, out var entry),
                Is.True);
            Assert.That(entry.SourcePackId.Value, Is.EqualTo("test.pack.m1"));
        }

        [UnityTest]
        public IEnumerator BootstrapComposesQinglanUiInputAndPresentation()
        {
            yield return LoadBootstrapScene();
            var host = Object.FindFirstObjectByType<QinglanDemoRuntimeHost>();

            Assert.That(host, Is.Not.Null);
            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.TitleProfile));
            Assert.That(host.Input.UiMap.enabled, Is.True);
            Assert.That(host.Input.GameplayMap.enabled, Is.False);
            Assert.That(host.Presentation.ActiveViewCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BootstrapperRejectsDuplicateInstance()
        {
            yield return LoadBootstrapScene();
            LogAssert.Expect(
                LogType.Warning,
                "[Bootstrap] Duplicate GameBootstrapper rejected.");

            var duplicate = new GameObject("DuplicateGameBootstrapper");
            duplicate.AddComponent<GameBootstrapper>();
            yield return null;

            var all = Object.FindObjectsByType<GameBootstrapper>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(all, Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NullPlatformFacadeIsUsable()
        {
            yield return LoadBootstrapScene();
            var platform = GetOnlyBootstrapper().PlatformFacade;

            Assert.That(platform, Is.TypeOf<NullPlatformFacade>());
            Assert.That(platform.IsAvailable, Is.False);
            Assert.That(platform.Achievements, Is.Not.Null);
            Assert.That(platform.Stats, Is.Not.Null);
            Assert.That(platform.Cloud, Is.Not.Null);
            Assert.That(platform.RichPresence, Is.Not.Null);
            Assert.That(platform.Identity, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator StartupProducesNoUnhandledErrorOrException()
        {
            var errors = 0;
            void Capture(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error ||
                    type == LogType.Exception ||
                    type == LogType.Assert)
                {
                    errors++;
                }
            }

            UnityEngine.Application.logMessageReceived += Capture;
            try
            {
                yield return LoadBootstrapScene();
                yield return null;
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= Capture;
            }

            Assert.That(errors, Is.Zero);
        }

        private static IEnumerator LoadBootstrapScene()
        {
            var operation = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        private static GameBootstrapper GetOnlyBootstrapper()
        {
            var all = Object.FindObjectsByType<GameBootstrapper>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(all, Has.Length.EqualTo(1));
            return all[0];
        }
    }
}
