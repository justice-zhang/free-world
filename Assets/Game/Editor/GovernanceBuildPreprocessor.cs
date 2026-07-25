using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    internal sealed class GovernanceBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var validation = ProjectGovernanceValidator.ValidateCurrentProject();
            if (!validation.IsValid)
            {
                throw new BuildFailedException(validation.Issues[0].ToString());
            }
        }
    }
}
