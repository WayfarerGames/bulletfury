using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wayfarer_Games.Common
{
    public static class AssetPathUtility
    {
        private static readonly string[] RootPaths =
        {
            "PackageSource/com.wayfarergames.bulletfury",
            "Packages/com.wayfarergames.bulletfury",
            "Assets/Wayfarer Games"
        };

#if UNITY_EDITOR
        public static T LoadAssetAtKnownRoots<T>(string relativePath) where T : Object
        {
            foreach (var rootPath in RootPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>($"{rootPath}/{relativePath}");
                if (asset != null)
                    return asset;
            }

            return null;
        }
#endif
    }
}
