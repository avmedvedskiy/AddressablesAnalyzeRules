using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    public class RemoteDependenciesAnalyzeRule : BaseDependenciesAnalyzeRule
    {
        public override bool CanFix => false;
        public override string ruleName => "Check if local bundles has remote dependencies";

        protected override void WriteAssetsAndGroups(List<GroupDependencyData> resultDependencies)
        {
            WriteErrorBundleGroups(resultDependencies);
            foreach (var result in resultDependencies)
            {
                var severity = result.IsDependency ? MessageType.Error : MessageType.None;
                foreach (var analyzeResult in result.ToAnalyzeResult(severity))
                    AddResult(analyzeResult);
            }
        }

        private void WriteErrorBundleGroups(List<GroupDependencyData> resultDependencies)
        {
            var errorGroups = resultDependencies.Where(x => x.IsDependency);
            foreach (var result in errorGroups)
            {
                AddResult(new AnalyzeResult()
                {
                    resultName =
                        $"{result.BundleName} has dependency",
                    severity = MessageType.Error
                });
            }
        }

        protected override bool IsDependency(AddressableAssetGroup group, AddressableAssetGroup dependencyGroup)
        {
            var loadPath = GetLoadPathFromGroup(group);
            var loadPathDependency = GetLoadPathFromGroup(dependencyGroup);
            return !loadPath.Contains("http") && loadPathDependency.Contains("http");
        }

        private static string GetLoadPathFromGroup(AddressableAssetGroup group)
        {
            var groupSchema = group.GetSchema<BundledAssetGroupSchema>();
            return groupSchema.LoadPath.GetValue(group.Settings);
        }

        [InitializeOnLoad]
        class RegisterRemoteDependenciesAnalyzeRule
        {
            static RegisterRemoteDependenciesAnalyzeRule()
            {
                AnalyzeSystem.RegisterNewRule<RemoteDependenciesAnalyzeRule>();
            }
        }
    }
}