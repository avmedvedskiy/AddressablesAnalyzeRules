using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using Object = System.Object;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    public class TextureAssetData
    {
        public string AssetBundleName { get; set; }
        public bool Explicit { get; set; }
        public string AssetPath { get; set; }
    }

    [Serializable]
    public class TextureCompressionAnalyzeRule : BundleRuleBase
    {
        private readonly List<AnalyzeResult> _ruleResults = new();
        private readonly AtlasFinder _atlasFinder = new();
        private readonly List<TextureAssetData> _resultData = new();
        private string _defaultCompression;

        public override bool CanFix => false;
        public override string ruleName => "Check textures without compression";

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
                _defaultCompression = GetDefaultTextureCompressionFormat();
                var context = GetBuildContext(settings);
                ReturnCode exitCode = RefreshBuild(context);
                if (exitCode < ReturnCode.Success)
                {
                    Debug.LogError("Analyze build failed. " + exitCode);
                    _ruleResults.Add(new AnalyzeResult { resultName = ruleName + "Analyze build failed. " + exitCode });
                    return _ruleResults;
                }

                CollectAllTextures(context);
                WriteResults();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            else
            {
                _ruleResults.Add(noErrors);
            }

            return _ruleResults;
        }

        public override void ClearAnalysis()
        {
            base.ClearAnalysis();
            _ruleResults.Clear();
            _resultData.Clear();
        }

        private void CollectAllTextures(AddressableAssetsBuildContext context)
        {
            ConvertBundleNamesToGroupNames(context);
            foreach (var bundleBuild in AllBundleInputDefs)
            {
                foreach (string assetName in bundleBuild.assetNames)
                {
                    AddOnlyTextures(bundleBuild.assetBundleName, assetName, true);
                }
            }

            foreach (KeyValuePair<string, string> fileToBundle in ExtractData.WriteData.FileToBundle)
            {
                string assetBundleName = fileToBundle.Value;

                var implicitGuids = GetImplicitGuidsForBundle(fileToBundle.Key);
                foreach (GUID guid in implicitGuids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid.ToString());
                    if (AddressableAssetUtility.IsPathValidForEntry(assetPath))
                    {
                        AddOnlyTextures(assetBundleName, assetPath, false);
                    }
                }
            }
        }

        private void AddOnlyTextures(string assetBundleName, string assetPath, bool explicitAsset)
        {
            var assetImporter = AssetImporter.GetAtPath(assetPath);
            if (assetImporter is TextureImporter textureImporter)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                if (!IsPow2(texture) && !HasCompression(textureImporter) && !_atlasFinder.HasAtlas(assetPath, out _))
                {
                    _resultData.Add(new TextureAssetData()
                    {
                        AssetBundleName = assetBundleName,
                        AssetPath = assetPath,
                        Explicit = explicitAsset
                    });
                }
            }
        }

        private bool HasCompression(TextureImporter textureImporter)
        {
            var settings = textureImporter.GetPlatformTextureSettings(GetCurrentPlatformName());
            return settings.overridden && settings.format != TextureImporterFormat.Automatic
                ? settings.format.ToString().Contains("ASTC")
                : _defaultCompression.Contains("ASTC");
        }

        private string GetDefaultTextureCompressionFormat()
        {
            MethodInfo getTextureCompressionMethod =
                typeof(PlayerSettings).GetMethod("GetDefaultTextureCompressionFormat",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            return getTextureCompressionMethod?.Invoke(null, new object[] { GetCurrentBuildTargetGroup() }).ToString();
        }

        private BuildTargetGroup GetCurrentBuildTargetGroup() =>
            EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.Android => BuildTargetGroup.Android,
                BuildTarget.iOS => BuildTargetGroup.iOS,
                BuildTarget.WebGL => BuildTargetGroup.WebGL,
                BuildTarget.StandaloneWindows or
                    BuildTarget.StandaloneWindows64 or
                    BuildTarget.StandaloneLinux64 or
                    BuildTarget.StandaloneOSX => BuildTargetGroup.Standalone,
                _ => throw new Exception("Not Found Current Platform")
            };


        private string GetCurrentPlatformName() =>
            EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "iOS",
                BuildTarget.WebGL => "WebGl",
                BuildTarget.StandaloneWindows or
                    BuildTarget.StandaloneWindows64 or
                    BuildTarget.StandaloneLinux64 or
                    BuildTarget.StandaloneOSX => "Standalone",
                _ => throw new Exception("Not Found Current Platform")
            };

        private bool IsPow2(Texture texture)
        {
            return IsPow2(texture.width) && IsPow2(texture.height);
        }

        private bool IsPow2(int value) => (value & (value - 1)) == 0 && value > 0;

        private void WriteResults()
        {
            if (_resultData.Count == 0)
            {
                _ruleResults.Add(noErrors);
                return;
            }

            foreach (var result in _resultData)
            {
                _ruleResults.Add(new AnalyzeResult
                {
                    resultName = result.AssetBundleName + kDelimiter
                                                        + (result.Explicit ? "Explicit" : "Implicit") + kDelimiter +
                                                        result.AssetPath,
                    severity = MessageType.Warning
                });
            }
        }

        [InitializeOnLoad]
        class RegisterTextureCompressionAnalyzeRule
        {
            static RegisterTextureCompressionAnalyzeRule()
            {
                AnalyzeSystem.RegisterNewRule<TextureCompressionAnalyzeRule>();
            }
        }
    }
}