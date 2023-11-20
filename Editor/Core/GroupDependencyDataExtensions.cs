using System.Collections.Generic;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    public static class GroupDependencyDataExtensions
    {
        public static IEnumerable<AnalyzeRule.AnalyzeResult> ToAnalyzeResult(this GroupDependencyData data, MessageType messageType)
        {
            yield return new AnalyzeRule.AnalyzeResult()
            {
                resultName =
                    $"{data.BundleName} has dependency{AnalyzeRule.kDelimiter}{data.DependencyBundleName}{AnalyzeRule.kDelimiter}",
                severity = messageType
            };

            foreach (var assetPath in data.AssetPathList)
            {
                //main asset
                yield return new AnalyzeRule.AnalyzeResult()
                {
                    resultName =
                        $"{data.BundleName} has dependency{AnalyzeRule.kDelimiter}{data.DependencyBundleName}{AnalyzeRule.kDelimiter}{assetPath.Key}{AnalyzeRule.kDelimiter}",
                    severity = messageType
                };

                //and all subassets deep
                foreach (var subAssetPath in assetPath.Value)
                {
                    yield return new AnalyzeRule.AnalyzeResult()
                    {
                        resultName =
                            $"{data.BundleName} has dependency{AnalyzeRule.kDelimiter}{data.DependencyBundleName}{AnalyzeRule.kDelimiter}{assetPath.Key}{AnalyzeRule.kDelimiter}{subAssetPath}",
                        severity = messageType
                    };
                }
            }
        }
    }
}