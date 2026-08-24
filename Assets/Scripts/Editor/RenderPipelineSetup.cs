using UnityEditor;
using UnityEngine;

namespace Graft.Editor
{
    /// <summary>
    /// Tunes the two URP assets the project inherited from Unity's 3D template.
    ///
    /// The template ships a 50 m shadow distance, which is sized for a first-person view. A
    /// top-down camera only ever sees about 20 m of ground, so 50 m spends the whole shadow map on
    /// geometry that is off screen and the shadows that ARE on screen come out blocky.
    /// </summary>
    public static class RenderPipelineSetup
    {
        public const string MobileAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
        public const string PCAssetPath = "Assets/Settings/PC_RPAsset.asset";

        /// <summary>Metres of shadow. The top-down camera never sees further than this.</summary>
        public const float ShadowDistance = 25f;

        public static void Configure()
        {
            // One cascade, not the template's four. Cascades exist to spread resolution over a
            // long view; a fixed-height top-down camera has no long view, and every split leaves a
            // visible diagonal seam across otherwise flat ground. 2048 texels over 25 m is plenty.
            Tune(MobileAssetPath, shadowmapResolution: 2048, cascades: 1, softShadowQuality: 2);
            Tune(PCAssetPath, shadowmapResolution: 2048, cascades: 1, softShadowQuality: 3);

            AssetDatabase.SaveAssets();
        }

        static void Tune(string path, int shadowmapResolution, int cascades, int softShadowQuality)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[GRAFT] Render pipeline asset missing: {path}");
                return;
            }

            var serialized = new SerializedObject(asset);
            Set(serialized, "m_ShadowDistance", p => p.floatValue = ShadowDistance);
            Set(serialized, "m_MainLightShadowsSupported", p => p.boolValue = true);
            Set(serialized, "m_MainLightShadowmapResolution", p => p.intValue = shadowmapResolution);
            Set(serialized, "m_ShadowCascadeCount", p => p.intValue = cascades);
            Set(serialized, "m_SoftShadowsSupported", p => p.boolValue = true);
            Set(serialized, "m_SoftShadowQuality", p => p.intValue = softShadowQuality);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void Set(SerializedObject serialized, string field, System.Action<SerializedProperty> apply)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[GRAFT] URP field '{field}' not found - the package layout changed.");
                return;
            }

            apply(property);
        }
    }
}
