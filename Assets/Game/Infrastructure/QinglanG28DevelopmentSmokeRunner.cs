using System;
using System.Collections;
using System.Globalization;
using System.IO;
using Game.Application;
using Game.Presentation;
using Game.Simulation;
using Game.UI;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Opt-in Development Player smoke driver for the real Bootstrap scene. It uses the
    /// public UI/application commands and exists only to make the built Player gate repeatable.
    /// </summary>
    internal sealed class QinglanG28DevelopmentSmokeRunner : MonoBehaviour
    {
        private const string Argument = "-qinglanG28Smoke";

        internal static bool IsRequested()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
                if (string.Equals(arguments[index], Argument, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private IEnumerator Start()
        {
            yield return null;
            var host = GetComponent<QinglanDemoRuntimeHost>();
            var result = new QinglanG28PlayerSmokeResult
            {
                schemaVersion = 1,
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                status = "FAIL"
            };
            if (host == null)
            {
                Finish(result, "The Qinglan runtime host is missing.");
                yield break;
            }

            result.titleVisited = host.Flow.Stage == DemoFlowStage.Title;
            if (!result.titleVisited || !host.Flow.Execute(QinglanUiCommand.Start, "start", 0))
            {
                Finish(result, "Title to character selection failed.");
                yield break;
            }
            result.characterSelectVisited = host.Flow.Stage == DemoFlowStage.CharacterSelect;
            if (!host.Flow.Execute(QinglanUiCommand.Continue, "character", 0) ||
                !host.Flow.Execute(QinglanUiCommand.OpenLoadout, "map", 0))
            {
                Finish(result, "Character/map/loadout selection failed.");
                yield break;
            }
            result.mapAndLoadoutVisited = host.Flow.Stage == DemoFlowStage.MapSelect;
            if (!host.Flow.Execute(QinglanUiCommand.BeginRun, "begin", 0))
            {
                Finish(result, "Run preparation failed.");
                yield break;
            }
            host.TickRuntime(0d);
            host.TickRuntime(SimulationClock.TickDurationSeconds);
            result.activeRunVisited = host.Flow.Stage == DemoFlowStage.Active;
            result.activeViews = host.Presentation.ActiveViewCount;
            if (!result.activeRunVisited || result.activeViews <= 0 || !host.Flow.TogglePause() ||
                host.Flow.Stage != DemoFlowStage.UserPaused || !host.Flow.TogglePause())
            {
                Finish(result, "Active run or pause/resume flow failed.");
                yield break;
            }
            result.pauseResumeVisited = host.Flow.Stage == DemoFlowStage.Active;

            host.Flow.Settings.SetColorVision(ColorVisionMode.HighContrast);
            host.Flow.Settings.SetFlashIntensity(0.25f);
            host.Flow.Settings.SetDamageNumbersEnabled(false);
            result.accessibilityApplied = host.Flow.Settings.ColorVision ==
                                          ColorVisionMode.HighContrast &&
                                          !host.Flow.Settings.DamageNumbersEnabled;
            for (var attempt = 0; attempt < 8 && host.Flow.Stage == DemoFlowStage.Active; attempt++)
            {
                host.Flow.DebugRequestLevelUp();
                host.TickRuntime(SimulationClock.TickDurationSeconds);
            }
            result.upgradeVisited = host.Flow.Stage == DemoFlowStage.UpgradePaused;
            if (!result.upgradeVisited ||
                !host.Flow.Execute(QinglanUiCommand.SelectUpgrade, "upgrade", 0))
            {
                Finish(result, "Upgrade choice flow failed.");
                yield break;
            }
            host.TickRuntime(0d);
            if (!host.Flow.DebugCompleteRun())
            {
                Finish(result, "Development completion command failed.");
                yield break;
            }
            host.TickRuntime(0d);
            result.resultVisited = host.Flow.Stage == DemoFlowStage.Result;
            for (var attempt = 0; attempt < 240 && !host.Flow.LastCommit.IsSuccess; attempt++)
            {
                host.TickRuntime(0.05d);
                yield return null;
            }
            result.saveCommitted = host.Flow.LastCommit.IsSuccess;
            if (!result.resultVisited || !result.saveCommitted ||
                !host.Flow.Execute(QinglanUiCommand.ContinueToHub, "hub", 0))
            {
                Finish(result, "Result settlement or hub transition failed.");
                yield break;
            }
            host.TickRuntime(0d);
            result.hubVisited = host.Flow.Stage == DemoFlowStage.Hub;
            result.activeViewsAfterHub = host.Presentation.ActiveViewCount;
            result.vfxCreated = host.Presentation.CreatedVfxCount;
            result.audioSourcesCreated = host.Presentation.CreatedAudioSourceCount;
            if (!result.hubVisited || result.activeViewsAfterHub != 0 ||
                !host.Flow.Execute(QinglanUiCommand.StartAgain, "again", 0))
            {
                Finish(result, "Hub cleanup or restart failed.");
                yield break;
            }
            result.restartVisited = host.Flow.Stage == DemoFlowStage.CharacterSelect;
            result.inputOwnerCount = FindObjectsByType<M7InputRouter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var passed = result.titleVisited && result.characterSelectVisited &&
                         result.mapAndLoadoutVisited && result.activeRunVisited &&
                         result.pauseResumeVisited && result.accessibilityApplied &&
                         result.upgradeVisited && result.resultVisited && result.saveCommitted &&
                         result.hubVisited && result.restartVisited &&
                         result.activeViewsAfterHub == 0 && result.inputOwnerCount == 1 &&
                         result.vfxCreated <= 200 && result.audioSourcesCreated <= 32;
            result.status = passed ? "PASS" : "FAIL";
            result.error = passed ? string.Empty : "One or more Player smoke assertions failed.";
            WriteAndQuit(result, passed ? 0 : 2);
        }

        private static void Finish(QinglanG28PlayerSmokeResult result, string error)
        {
            result.status = "FAIL";
            result.error = error;
            WriteAndQuit(result, 2);
        }

        private static void WriteAndQuit(QinglanG28PlayerSmokeResult result, int exitCode)
        {
            try
            {
                var path = Environment.GetEnvironmentVariable("QINGLAN_G28_PLAYER_RESULT");
                if (string.IsNullOrWhiteSpace(path))
                    path = Path.Combine(UnityEngine.Application.persistentDataPath, "QinglanG28PlayerSmoke.json");
                path = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Invalid smoke result path.");
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonUtility.ToJson(result, true) + "\n");
                if (exitCode == 0) Debug.Log("[Qinglan G2.8 Player Smoke] PASS: " + path);
                else Debug.LogError("[Qinglan G2.8 Player Smoke] FAIL: " + result.error);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 3;
            }
            UnityEngine.Application.Quit(exitCode);
        }

        [Serializable]
        private sealed class QinglanG28PlayerSmokeResult
        {
            public int schemaVersion;
            public string status;
            public string error;
            public string generatedAtUtc;
            public bool titleVisited;
            public bool characterSelectVisited;
            public bool mapAndLoadoutVisited;
            public bool activeRunVisited;
            public bool pauseResumeVisited;
            public bool accessibilityApplied;
            public bool upgradeVisited;
            public bool resultVisited;
            public bool saveCommitted;
            public bool hubVisited;
            public bool restartVisited;
            public int activeViews;
            public int activeViewsAfterHub;
            public int inputOwnerCount;
            public int vfxCreated;
            public int audioSourcesCreated;
        }
    }
}
