using System;
using System.Globalization;
using System.IO;
using Game.Simulation;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Content-free framework Release smoke entry point. The generated M10 Release scene owns
    /// this component and exits after one deterministic simulation check.
    /// </summary>
    public sealed class M10ReleaseSmokeRunner : MonoBehaviour
    {
        private const ulong SmokeSeed = 0x4D3130534D4F4B45UL;

        private void Start()
        {
            var exitCode = 0;
            M10ReleaseSmokeResult result;
            try
            {
                var summary = HeadlessSimulationHarness.Run(60, SmokeSeed, 4);
                var passed = summary.TickCount == 60 &&
                             summary.ActorCount == 4 &&
                             summary.SnapshotEntityCount == 4 &&
                             summary.InvalidHandleAccesses == 0;
                result = new M10ReleaseSmokeResult
                {
                    schemaVersion = 1,
                    status = passed ? "PASS" : "FAIL",
                    seed = SmokeSeed.ToString(CultureInfo.InvariantCulture),
                    ticks = summary.TickCount,
                    actors = summary.ActorCount,
                    snapshotEntities = summary.SnapshotEntityCount,
                    invalidHandleAccesses = summary.InvalidHandleAccesses
                };
                if (!passed) exitCode = 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
                result = new M10ReleaseSmokeResult
                {
                    schemaVersion = 1,
                    status = "FAIL",
                    seed = SmokeSeed.ToString(CultureInfo.InvariantCulture),
                    error = exception.Message
                };
            }

            try
            {
                WriteResult(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            if (exitCode == 0) Debug.Log("[M10 Release Smoke] PASS");
            else Debug.LogError("[M10 Release Smoke] FAIL");
            UnityEngine.Application.Quit(exitCode);
        }

        private static void WriteResult(M10ReleaseSmokeResult result)
        {
            var path = Environment.GetEnvironmentVariable("M10_SMOKE_RESULT");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(UnityEngine.Application.persistentDataPath, "M10ReleaseSmoke.json");
            path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Invalid smoke result path.");
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(result, true) + "\n");
        }

        [Serializable]
        private sealed class M10ReleaseSmokeResult
        {
            public int schemaVersion;
            public string status;
            public string seed;
            public long ticks;
            public int actors;
            public int snapshotEntities;
            public long invalidHandleAccesses;
            public string error;
        }
    }
}
