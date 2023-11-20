using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    public static class ContextExtensions
    {
        public static bool IsAssetInGroup(this AddressableAssetsBuildContext context, string guid,
            AddressableAssetGroup group)
        {
            var entry = context.assetEntries.Find(x => x.guid == guid);
            return entry != null && entry.parentGroup == group;
        }
    }
}