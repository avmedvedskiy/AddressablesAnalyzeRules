using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    public class AllDependenciesAnalyzeRule : BaseDependenciesAnalyzeRule
    {
        public override bool CanFix => false;
        public override string ruleName => "All Bundle cross dependencies";

        protected override void WriteAssetsAndGroups(List<GroupDependencyData> resultDependencies)
        {
            foreach (var result in resultDependencies)
            {
                var severity = MessageType.None;
                foreach (var analyzeResult in result.ToAnalyzeResult(severity))
                    AddResult(analyzeResult);
            }
        }

        protected override bool IsDependency(AddressableAssetGroup group, AddressableAssetGroup dependencyGroup)
        {
            return true;
        }

        [InitializeOnLoad]
        class RegisterAllDependenciesAnalyzeRule
        {
            static RegisterAllDependenciesAnalyzeRule()
            {
                AnalyzeSystem.RegisterNewRule<AllDependenciesAnalyzeRule>();
            }
        }
    }
}