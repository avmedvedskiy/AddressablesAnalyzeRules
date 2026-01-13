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
                ? GetCompressionFamily(settings.format) 
                    is TextureCompressionFamily.ASTC 
                    or TextureCompressionFamily.Uncompressed
                : _defaultCompression.Contains("ASTC");
        }
        
        public TextureCompressionFamily GetCompressionFamily(TextureImporterFormat f)
        {
            // Automatic / legacy “automatic*”
            if ((int)f < 0)
                return TextureCompressionFamily.Automatic;

            switch (f)
            {
                // --- DXT / BC family ---
                case TextureImporterFormat.DXT1:
                case TextureImporterFormat.DXT5:
                case TextureImporterFormat.DXT1Crunched:
                case TextureImporterFormat.DXT5Crunched:
                case TextureImporterFormat.BC4:
                case TextureImporterFormat.BC5:
                case TextureImporterFormat.BC6H:
                case TextureImporterFormat.BC7:
                    return TextureCompressionFamily.DXT_BC;

                // --- PVRTC ---
                case TextureImporterFormat.PVRTC_RGB2:
                case TextureImporterFormat.PVRTC_RGBA2:
                case TextureImporterFormat.PVRTC_RGB4:
                case TextureImporterFormat.PVRTC_RGBA4:
                    return TextureCompressionFamily.PVRTC;

                // --- ETC / ETC2 / EAC (и их crunched) ---
                case TextureImporterFormat.ETC_RGB4:
                case TextureImporterFormat.ETC_RGB4Crunched:
                case TextureImporterFormat.ETC2_RGB4:
                case TextureImporterFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA:
                case TextureImporterFormat.ETC2_RGBA8:
                case TextureImporterFormat.ETC2_RGBA8Crunched:
                case TextureImporterFormat.EAC_R:
                case TextureImporterFormat.EAC_R_SIGNED:
                case TextureImporterFormat.EAC_RG:
                case TextureImporterFormat.EAC_RG_SIGNED:
                    return TextureCompressionFamily.ETC_EAC;

                // --- ASTC LDR ---
                case TextureImporterFormat.ASTC_4x4:
                case TextureImporterFormat.ASTC_5x5:
                case TextureImporterFormat.ASTC_6x6:
                case TextureImporterFormat.ASTC_8x8:
                case TextureImporterFormat.ASTC_10x10:
                case TextureImporterFormat.ASTC_12x12:
                // --- ASTC HDR ---
                case TextureImporterFormat.ASTC_HDR_4x4:
                case TextureImporterFormat.ASTC_HDR_5x5:
                case TextureImporterFormat.ASTC_HDR_6x6:
                case TextureImporterFormat.ASTC_HDR_8x8:
                case TextureImporterFormat.ASTC_HDR_10x10:
                case TextureImporterFormat.ASTC_HDR_12x12:
                    return TextureCompressionFamily.ASTC;

                // --- Everything else is not “a compression family” ---
                default:
                    return TextureCompressionFamily.Uncompressed;
            }
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
        
        public enum TextureCompressionFamily
        {
            Unknown = 0,

            // Not actually compressed (raw / packed / float / integer formats)
            Uncompressed,

            // Block compression families (desktop / modern)
            DXT_BC,     // DXT1/DXT5 + BC4/5/6H/7

            // Mobile families
            ETC_EAC,    // ETC, ETC2, EAC (including crunched variants)
            PVRTC,      // PVRTC RGB/RGBA 2/4bpp
            ASTC,       // ASTC LDR (all block sizes)

            // Unity “Automatic*”
            Automatic,
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