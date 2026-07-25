using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ContentIdTests
    {
        [TestCase("base.skill.arc_bolt", "base.skill.arc_bolt")]
        [TestCase("  BASE.Skill.Arc_Bolt  ", "base.skill.arc_bolt")]
        [TestCase("test.map.arena2", "test.map.arena2")]
        public void CreateNormalizesValidNamespacedStrings(string raw, string expected)
        {
            var result = ContentId.Create(raw);

            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            Assert.That(result.Value.Value, Is.EqualTo(expected));
            Assert.That(result.Value.ToString(), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("single")]
        [TestCase(".base.skill")]
        [TestCase("base..skill")]
        [TestCase("base.skill.")]
        [TestCase("base.skill-name")]
        [TestCase("base.skill__name")]
        [TestCase("base.skill/other")]
        public void CreateRejectsInvalidSamples(string raw)
        {
            var result = ContentId.Create(raw);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.InvalidContentId));
        }

        [Test]
        public void CaseVariantsNormalizeToTheSameComparableValue()
        {
            var lower = ContentId.Create("base.skill.pulse").Value;
            var upper = ContentId.Create("BASE.SKILL.PULSE").Value;

            Assert.That(lower, Is.EqualTo(upper));
            Assert.That(lower.CompareTo(upper), Is.Zero);
        }

        [Test]
        public void StringSerializationRoundTripPreservesCanonicalId()
        {
            var original = ContentId.Create("base.character.test_runner").Value;

            var restored = ContentId.Deserialize(original.Serialize());

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value, Is.EqualTo(original));
            Assert.That(restored.Value.Value, Is.EqualTo("base.character.test_runner"));
        }

        [Test]
        public void StableHashCollisionDoesNotChangeIdentityOrDictionaryLookup()
        {
            var first = ContentId.Create("test.collision.3629").Value;
            var second = ContentId.Create("test.collision.21d94").Value;
            var dictionary = new Dictionary<ContentId, string>
            {
                { first, "first" },
                { second, "second" }
            };

            Assert.That(first.StableHash, Is.EqualTo(second.StableHash));
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(dictionary, Has.Count.EqualTo(2));
            Assert.That(dictionary[first], Is.EqualTo("first"));
            Assert.That(dictionary[second], Is.EqualTo("second"));
        }
    }
}
