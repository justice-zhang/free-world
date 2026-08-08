using System.Collections;
using Game.Application;
using Game.Infrastructure;
using Game.Presentation;
using Game.Simulation;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class QinglanG27PresentationPolishPlayModeTests
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
        public IEnumerator RealRunBuildsMapDistinctPlayerSilhouetteAndBoundedPresentationPools()
        {
            var host = Object.FindFirstObjectByType<QinglanDemoRuntimeHost>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.Flow.Execute(QinglanUiCommand.Start, "start", 0), Is.True);
            Assert.That(host.Flow.Execute(QinglanUiCommand.Continue, "character", 0), Is.True);
            Assert.That(host.Flow.Execute(QinglanUiCommand.OpenLoadout, "map", 0), Is.True);
            Assert.That(host.Flow.Execute(QinglanUiCommand.BeginRun, "begin", 0), Is.True);
            host.TickRuntime(0d);
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            yield return null;

            Assert.That(host.Flow.Stage, Is.EqualTo(DemoFlowStage.Active));
            Assert.That(host.Presentation.MapMarkerCount, Is.EqualTo(11));
            Assert.That(GameObject.Find("G2_7_ProceduralMap"), Is.Not.Null);
            Assert.That(host.Presentation.TryGetView(host.Flow.Session.Player, out var player), Is.True);
            Assert.That(player.Shape, Is.EqualTo(ProceduralShape.Triangle));
            var standardColor = player.DisplayColor;

            host.Flow.Settings.SetColorVision(ColorVisionMode.HighContrast);
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            Assert.That(player.Shape, Is.EqualTo(ProceduralShape.Triangle));
            Assert.That(player.DisplayColor, Is.Not.EqualTo(standardColor));

            for (var index = 0; index < 360; index++)
                host.TickRuntime(SimulationClock.TickDurationSeconds);
            yield return null;

            Assert.That(host.Presentation.CreatedVfxCount, Is.LessThanOrEqualTo(200));
            Assert.That(host.Presentation.CreatedAudioSourceCount, Is.LessThanOrEqualTo(32));
            Assert.That(host.Presentation.ActiveVfxCount, Is.LessThanOrEqualTo(200));
            Assert.That(host.Presentation.ActiveAudioCount, Is.LessThanOrEqualTo(32));
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
