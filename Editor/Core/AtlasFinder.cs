using System.Collections.Generic;
using System.Linq;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    public class AtlasFinder
    {
        private readonly Dictionary<string, SpriteAtlas> _loadedAtlases = new();
        private string[] _atlasesGUIDs;

        public bool HasAtlas(string path, out string atlasGUID)
        {
            var assetImporter = AssetImporter.GetAtPath(path);
            if (assetImporter is TextureImporter { textureType: TextureImporterType.Sprite })
            {
                _atlasesGUIDs ??= AssetDatabase.FindAssets("t:spriteatlas");
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                for (int i = 0; i < _atlasesGUIDs.Length; i++)
                {
                    atlasGUID = _atlasesGUIDs[i];
                    if (!_loadedAtlases.TryGetValue(atlasGUID, out var atlas))
                    {
                        atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(atlasGUID));
                        _loadedAtlases.Add(atlasGUID, atlas);
                    }
                

                    if (atlas != null && atlas.CanBindTo(sprite))
                        return true;
                }
            }
            atlasGUID = null;
            return false;
        }
        
        
        //var allAtlasGUIDs = AssetDatabase
        //    .FindAssets("t:spriteatlas")
        //    .Select(AssetDatabase.GUIDToAssetPath)
        //    .Select(AssetImporter.GetAtPath)
        //    .Cast<SpriteAtlasImporter>();
        
    }
}