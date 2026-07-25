using System;
using System.IO;
using Game.Editor;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ProjectGovernanceValidatorTests
    {
        [Test]
        public void ValidateDetectsUnregisteredThirdPartySample()
        {
            var temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "free-world-m0-validator-" + Guid.NewGuid().ToString("N"));

            try
            {
                var thirdParty = Path.Combine(temporaryRoot, "Assets", "ThirdParty");
                var ai = Path.Combine(temporaryRoot, "Assets", "GameAssets", "AI");
                Directory.CreateDirectory(thirdParty);
                Directory.CreateDirectory(ai);
                File.WriteAllText(Path.Combine(temporaryRoot, "THIRD_PARTY_NOTICES.md"), "# Empty");
                File.WriteAllText(
                    Path.Combine(temporaryRoot, "ASSET_PROVENANCE.csv"),
                    "asset_id,relative_path");
                File.WriteAllBytes(
                    Path.Combine(thirdParty, "unregistered.bin"),
                    new byte[] { 1, 2, 3 });

                var report = ProjectGovernanceValidator.Validate(temporaryRoot);

                Assert.That(report.IsValid, Is.False);
                Assert.That(
                    ContainsIssue(report, "M0-THIRDPARTY-UNREGISTERED"),
                    Is.True);
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, true);
                }
            }
        }

        private static bool ContainsIssue(ValidationReport report, string code)
        {
            for (var index = 0; index < report.Issues.Count; index++)
            {
                if (string.Equals(
                    report.Issues[index].Code,
                    code,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
