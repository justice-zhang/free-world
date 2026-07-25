using System;
using Game.Content.Runtime;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ContentPackTopologyTests
    {
        private static readonly ContentVersion GameVersion = new ContentVersion(0, 1, 0);

        [Test]
        public void SortPlacesDependenciesBeforeConsumersAndPreservesStableTies()
        {
            var basePack = Manifest("test.pack.base");
            var addon = Manifest(
                "test.pack.addon",
                Dependency("test.pack.base"));
            var independent = Manifest("test.pack.independent");

            var result = ContentPackTopology.Sort(
                new[] { addon, independent, basePack },
                GameVersion);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(
                Array.ConvertAll(result.Value, manifest => manifest.PackId.Value),
                Is.EqualTo(
                    new[]
                    {
                        "test.pack.independent",
                        "test.pack.base",
                        "test.pack.addon"
                    }));
        }

        [Test]
        public void SortFailsWithLocatableMissingDependency()
        {
            var addon = Manifest(
                "test.pack.addon",
                Dependency("test.pack.missing"));

            var result = ContentPackTopology.Sort(new[] { addon }, GameVersion);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.MissingDependency));
            Assert.That(result.Error.PackId.Value, Is.EqualTo("test.pack.addon"));
            Assert.That(result.Error.AuthorAssetPath, Does.Contain("test.pack.addon"));
        }

        [Test]
        public void SortFailsWhenDependenciesFormCycle()
        {
            var first = Manifest("test.pack.first", Dependency("test.pack.second"));
            var second = Manifest("test.pack.second", Dependency("test.pack.first"));

            var result = ContentPackTopology.Sort(new[] { first, second }, GameVersion);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.DependencyCycle));
            Assert.That(result.Error.Message, Does.Contain("test.pack.first"));
            Assert.That(result.Error.Message, Does.Contain("test.pack.second"));
        }

        [Test]
        public void SortFailsWhenDependencyVersionIsIncompatible()
        {
            var dependency = new ContentPackDependency(
                Id("test.pack.base"),
                new ContentVersion(2, 0, 0));
            var addon = Manifest("test.pack.addon", dependency);
            var basePack = Manifest("test.pack.base");

            var result = ContentPackTopology.Sort(
                new[] { addon, basePack },
                GameVersion);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.IncompatibleVersion));
        }

        private static ContentPackManifest Manifest(
            string id,
            params ContentPackDependency[] dependencies)
        {
            return new ContentPackManifest(
                Id(id),
                new ContentVersion(0, 1, 0),
                1,
                GameVersion,
                null,
                dependencies,
                "packs/" + id + "/catalog",
                "pack." + id,
                false,
                "Assets/Test/" + id + ".asset");
        }

        private static ContentPackDependency Dependency(string id)
        {
            return new ContentPackDependency(Id(id), new ContentVersion(0, 1, 0));
        }

        private static ContentId Id(string value)
        {
            return ContentId.Create(value).Value;
        }
    }
}
