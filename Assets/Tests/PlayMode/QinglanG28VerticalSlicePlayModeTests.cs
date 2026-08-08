using System.Collections;
using Game.Application;
using Game.Infrastructure;
using Game.Presentation;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class QinglanG28VerticalSlicePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyBootstrapInstances();
            yield return null;
            var operation = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyBootstrapInstances();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TenConsecutiveRealHostsReleaseRunViewsAndKeepOneInputOwner()
        {
            var host = Object.FindFirstObjectByType<QinglanDemoRuntimeHost>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Title));

            for (var run = 0; run < 10; run++)
            {
                if (run == 0)
                    Assert.That(host.Flow.Execute(QinglanUiCommand.Start, "start", 0), Is.True);
                Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.CharacterSelect));
                Assert.That(host.Flow.Execute(QinglanUiCommand.Continue, "character", 0), Is.True);
                Assert.That(host.Flow.Execute(QinglanUiCommand.OpenLoadout, "map", 0), Is.True);
                Assert.That(host.Flow.Execute(QinglanUiCommand.BeginRun, "begin", 0), Is.True);
                host.TickRuntime(0d);
                host.TickRuntime(1d / 30d);
                Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));
                Assert.That(host.Flow.Session, Is.Not.Null);
                Assert.That(host.Presentation.ActiveViewCount, Is.GreaterThan(0));

                Assert.That(host.Flow.DebugCompleteRun(), Is.True);
                host.TickRuntime(0d);
                Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Result));
                for (var attempt = 0; attempt < 120 && !host.Flow.LastCommit.IsSuccess; attempt++)
                {
                    host.TickRuntime(0.1d);
                    yield return null;
                }
                Assert.That(host.Flow.LastCommit.IsSuccess, Is.True,
                    host.Flow.LastCommit.Diagnostic.MessageKey);
                Assert.That(host.Flow.Execute(QinglanUiCommand.ContinueToHub, "hub", 0), Is.True);
                host.TickRuntime(0d);
                Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Hub));
                Assert.That(host.Flow.Session, Is.Null);
                Assert.That(host.Presentation.ActiveViewCount, Is.Zero);
                Assert.That(Object.FindObjectsByType<M7InputRouter>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<QinglanDemoRuntimeHost>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(host.Presentation.CreatedVfxCount, Is.LessThanOrEqualTo(200));
                Assert.That(host.Presentation.CreatedAudioSourceCount, Is.LessThanOrEqualTo(32));

                Assert.That(host.Flow.Execute(QinglanUiCommand.StartAgain, "again", 0), Is.True);
                host.TickRuntime(0d);
                Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.CharacterSelect));
            }

            Assert.That(host.Ui.CurrentPage, Is.EqualTo(QinglanUiPageId.CharacterSelect));
        }

        private static void DestroyBootstrapInstances()
        {
            var all = Object.FindObjectsByType<GameBootstrapper>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < all.Length; index++) Object.Destroy(all[index].gameObject);
        }
    }
}
