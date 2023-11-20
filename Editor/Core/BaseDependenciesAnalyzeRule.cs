using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    public class GroupDependencyData
    {
        public AddressableAssetGroup Group { get; set; }
        public AddressableAssetGroup DependencyGroup { get; set; }
        public string BundleName { get; set; }
        public string DependencyBundleName { get; set; }
        public bool IsDependency { get; set; }
        public Dictionary<string, List<string>> AssetPathList { get; set; } = new(); //assets with sub assets
    }

    public abstract class BaseDependenciesAnalyzeRule : BundleRuleBase
    {
        private readonly List<AnalyzeResult> _ruleResults = new();

        private List<GroupDependencyData> _resultList;
        private readonly AtlasFinder _atlasFinder = new();

        public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                _ruleResults.Add(new AnalyzeResult
                    { resultName = ruleName + "Cannot run Analyze with unsaved scenes" });
                return _ruleResults;
            }

            ClearAnalysis();
            CalculateInputDefinitions(settings);

            if (AllBundleInputDefs.Count > 0)
            {
                var context = GetBuildContext(settings);
                ReturnCode exitCode = RefreshBuild(context);
                if (exitCode < ReturnCode.Success)
                {
                    Debug.LogError("Analyze build failed. " + exitCode);
                    _ruleResults.Add(new AnalyzeResult { resultName = ruleName + "Analyze build failed. " + exitCode });
                    return _ruleResults;
                }

                FindAllBundlesDependencies(context);
                WriteResults();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            else
            {
                _ruleResults.Add(noErrors);
            }

            return _ruleResults;
        }

        private void FindAllBundlesDependencies(AddressableAssetsBuildContext context)
        {
            FindBundleDependencies(context);
            FindDependenciesAssets(context);
        }


        private void WriteResults()
        {
            if (_resultList.Count == 0)
            {
                AddResult(noErrors);
                return;
            }

            ConvertBundleNamesToGroupsInResults();
            WriteAssetsAndGroups(_resultList);
        }

        private void ConvertBundleNamesToGroupsInResults()
        {
            foreach (var result in _resultList)
            {
                var bundleName = ConvertBundleName(result.BundleName, result.Group.Name);
                var depsBundleName = ConvertBundleName(result.DependencyBundleName, result.DependencyGroup.Name);
                result.BundleName = bundleName;
                result.DependencyBundleName = depsBundleName;
            }
        }

        protected abstract void WriteAssetsAndGroups(List<GroupDependencyData> resultDependencies);

        private void FindDependenciesAssets(AddressableAssetsBuildContext context)
        {
            foreach (var asset in ExtractData.WriteData.AssetToFiles)
            {
                var depBundles = asset.Value.Select(x => ExtractData.WriteData.FileToBundle[x]).ToList();
                foreach (var data in _resultList)
                {
                    bool hasInDepBundles = depBundles.FirstOrDefault() == data.BundleName &&
                                           depBundles.Contains(data.DependencyBundleName);
                    if (hasInDepBundles && data.IsDependency)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(asset.Key);
                        data.AssetPathList.Add(assetPath,
                            FindDependenciesDeep(context, assetPath, data.DependencyGroup));
                    }
                }
            }
        }

        private List<string> FindDependenciesDeep(AddressableAssetsBuildContext context, string path,
            AddressableAssetGroup dependencyGroup)
        {
            List<string> deepPaths = new List<string>();
            var depPaths = AssetDatabase.GetDependencies(path, true);
            foreach (var deepPath in depPaths)
            {
                var guid = AssetDatabase.AssetPathToGUID(deepPath);
                if (context.IsAssetInGroup(guid, dependencyGroup))
                {
                    deepPaths.Add(deepPath);
                }
                else if (_atlasFinder.HasAtlas(deepPath, out var atlasGuid))
                {
                    if (context.IsAssetInGroup(atlasGuid, dependencyGroup))
                    {
                        deepPaths.Add(AssetDatabase.GUIDToAssetPath(atlasGuid));
                        deepPaths.Add(deepPath);
                    }
                }
            }

            return deepPaths;
        }

        private void FindBundleDependencies(AddressableAssetsBuildContext context)
        {
            foreach (var bundle in context.bundleToImmediateBundleDependencies)
            {
                if (bundle.Value.Count > 1) // because in list has self dependency 
                {
                    var group = GetGroup(context, bundle.Key);
                    for (var i = 1; i < bundle.Value.Count; i++)
                    {
                        var dependencyGroup = GetGroup(context, bundle.Value[i]);
                        if (group != null && dependencyGroup != null)
                        {
                            _resultList.Add(new GroupDependencyData()
                            {
                                BundleName = bundle.Key,
                                DependencyBundleName = bundle.Value[i],
                                Group = group,
                                DependencyGroup = dependencyGroup,
                                IsDependency = IsDependency(group, dependencyGroup)
                            });
                        }
                    }
                }
            }
        }

        protected abstract bool IsDependency(AddressableAssetGroup group, AddressableAssetGroup dependencyGroup);

        private AddressableAssetGroup GetGroup(AddressableAssetsBuildContext context, string bundleName)
        {
            if (context.bundleToAssetGroup.TryGetValue(bundleName, out var groupGuid))
            {
                return context.Settings.groups.Find(x => x.Guid == groupGuid);
            }

            return null;
        }

        protected void AddResult(AnalyzeResult result)
        {
            _ruleResults.Add(result);
        }

        public override void ClearAnalysis()
        {
            base.ClearAnalysis();
            _ruleResults?.Clear();
            _resultList?.Clear();
            _resultList ??= new();
        }
    }
}