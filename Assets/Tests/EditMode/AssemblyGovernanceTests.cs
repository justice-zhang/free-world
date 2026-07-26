using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class AssemblyGovernanceTests
    {
        private static readonly Dictionary<string, string[]> ExpectedReferences =
            new Dictionary<string, string[]>
            {
                { "Game.Core", Array.Empty<string>() },
                { "Game.Content.Runtime", new[] { "Game.Core" } },
                { "Game.Simulation", new[] { "Game.Core", "Game.Content.Runtime" } },
                { "Game.Platform.Abstractions", new[] { "Game.Core" } },
                {
                    "Game.Application",
                    new[]
                    {
                        "Game.Core",
                        "Game.Content.Runtime",
                        "Game.Simulation",
                        "Game.Platform.Abstractions"
                    }
                },
                {
                    "Game.Content.Authoring",
                    new[] { "Game.Core", "Game.Content.Runtime" }
                },
                {
                    "Game.Infrastructure",
                    new[]
                    {
                        "Game.Application",
                        "Game.Content.Runtime",
                        "Game.Core",
                        "Game.Simulation",
                        "Game.Presentation",
                        "Game.UI",
                        "Unity.InputSystem",
                        "Game.Platform.Abstractions",
                        "Game.Platform.Null"
                    }
                },
                {
                    "Game.Presentation",
                    new[]
                    {
                        "Game.Application",
                        "Game.Simulation",
                        "Game.Core",
                        "Unity.InputSystem",
                        "Unity.ugui"
                    }
                },
                { "Game.UI", new[] { "Game.Application", "Unity.ugui" } },
                { "Game.Platform.Null", new[] { "Game.Platform.Abstractions" } },
                {
                    "Game.Editor",
                    new[]
                    {
                        "Game.Core",
                        "Game.Content.Authoring",
                        "Game.Content.Runtime",
                        "Game.Infrastructure",
                        "Game.Presentation",
                        "Unity.InputSystem",
                        "Unity.Addressables",
                        "Unity.Addressables.Editor"
                    }
                },
                {
                    "Game.Tests.EditMode",
                    new[]
                    {
                        "Game.Core",
                        "Game.Content.Runtime",
                        "Game.Simulation",
                        "Game.Platform.Abstractions",
                        "Game.Application",
                        "Game.Content.Authoring",
                        "Game.Infrastructure",
                        "Game.Presentation",
                        "Game.UI",
                        "Game.Platform.Null",
                        "Game.Editor",
                        "Unity.InputSystem",
                        "Unity.Addressables.Editor"
                    }
                },
                {
                    "Game.Tests.PlayMode",
                    new[]
                    {
                        "Game.Application",
                        "Game.Content.Runtime",
                        "Game.Core",
                        "Game.Infrastructure",
                        "Game.Presentation",
                        "Game.Simulation",
                        "Game.UI",
                        "Game.Platform.Abstractions",
                        "Game.Platform.Null",
                        "Unity.InputSystem",
                        "Unity.InputSystem.TestFramework"
                    }
                }
            };

        [Test]
        public void RequiredAssembliesAreDiscoverableByUnityCompilationPipeline()
        {
            var discovered = new HashSet<string>(StringComparer.Ordinal);
            AddAssemblyNames(discovered, CompilationPipeline.GetAssemblies(AssembliesType.Player));
            AddAssemblyNames(discovered, CompilationPipeline.GetAssemblies(AssembliesType.Editor));

            var required = new[]
            {
                "Game.Core",
                "Game.Content.Runtime",
                "Game.Simulation",
                "Game.Platform.Abstractions",
                "Game.Application",
                "Game.Content.Authoring",
                "Game.Infrastructure",
                "Game.Presentation",
                "Game.UI",
                "Game.Platform.Null",
                "Game.Editor",
                "Game.Tests.EditMode",
                "Game.Tests.PlayMode"
            };

            for (var index = 0; index < required.Length; index++)
            {
                Assert.That(discovered, Does.Contain(required[index]), required[index]);
            }
        }

        [Test]
        public void AssemblyDefinitionsMatchApprovedAcyclicDependencyGraph()
        {
            var definitions = LoadGameAssemblyDefinitions();

            foreach (var expected in ExpectedReferences)
            {
                Assert.That(definitions, Contains.Key(expected.Key));
                CollectionAssert.AreEquivalent(
                    expected.Value,
                    definitions[expected.Key].references,
                    expected.Key);
            }

            AssertNoCycles(definitions);
            Assert.That(definitions["Game.Core"].noEngineReferences, Is.True);
            Assert.That(definitions["Game.Simulation"].noEngineReferences, Is.True);
        }

        [Test]
        public void SimulationSourcesDoNotUseUnityObjectOrSceneTypes()
        {
            var files = Directory.GetFiles(
                Path.GetFullPath("Assets/Game/Simulation"),
                "*.cs",
                SearchOption.AllDirectories);
            var prohibitedTokens = new[]
            {
                "UnityEngine",
                "GameObject",
                "MonoBehaviour",
                "SceneManager",
                "UnityEditor"
            };

            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                var source = File.ReadAllText(files[fileIndex]);
                for (var tokenIndex = 0; tokenIndex < prohibitedTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(prohibitedTokens[tokenIndex]),
                        files[fileIndex]);
                }
            }
        }

        private static void AddAssemblyNames(
            HashSet<string> destination,
            UnityEditor.Compilation.Assembly[] assemblies)
        {
            for (var index = 0; index < assemblies.Length; index++)
            {
                destination.Add(assemblies[index].name);
            }
        }

        private static Dictionary<string, AsmdefData> LoadGameAssemblyDefinitions()
        {
            var result = new Dictionary<string, AsmdefData>(StringComparer.Ordinal);
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");

            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    continue;
                }

                var data = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(path));
                if (data != null && data.name.StartsWith("Game.", StringComparison.Ordinal))
                {
                    data.references = data.references ?? Array.Empty<string>();
                    result[data.name] = data;
                }
            }

            return result;
        }

        private static void AssertNoCycles(Dictionary<string, AsmdefData> definitions)
        {
            var states = new Dictionary<string, VisitState>(StringComparer.Ordinal);
            foreach (var assemblyName in ExpectedReferences.Keys)
            {
                Visit(assemblyName, definitions, states);
            }
        }

        private static void Visit(
            string assemblyName,
            Dictionary<string, AsmdefData> definitions,
            Dictionary<string, VisitState> states)
        {
            if (states.TryGetValue(assemblyName, out var state))
            {
                Assert.That(state, Is.Not.EqualTo(VisitState.Visiting), "Cycle at " + assemblyName);
                return;
            }

            states[assemblyName] = VisitState.Visiting;
            var references = definitions[assemblyName].references;
            for (var index = 0; index < references.Length; index++)
            {
                if (ExpectedReferences.ContainsKey(references[index]))
                {
                    Visit(references[index], definitions, states);
                }
            }

            states[assemblyName] = VisitState.Visited;
        }

        [Serializable]
        private sealed class AsmdefData
        {
            public string name;
            public string[] references;
            public bool noEngineReferences;
        }

        private enum VisitState
        {
            Visiting,
            Visited
        }
    }
}
