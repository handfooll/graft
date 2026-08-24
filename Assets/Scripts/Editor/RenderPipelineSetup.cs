using Graft.Game;
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

        /// <summary>
        /// A little past the far edge of frame, so the boundary itself is never on screen.
        /// </summary>
        const float ShadowMargin = 1.15f;

        [MenuItem("GRAFT/Configure Render Pipeline")]
        static void ConfigureFromMenu() => Configure(ProjectSetup.EnsureCameraFraming());

        public static void Configure(CameraFraming framing)
        {
            // Derived from the camera, never a constant. A flat 25 m silently deleted every shadow
            // in the frame the moment the camera pulled back past it - no error, just no shadows.
            var shadowDistance = framing.FarGroundDistance * ShadowMargin;
            // One cascade, not the template's four. Cascades exist to spread resolution over a
            // long view; a fixed-height top-down camera has no long view, and every split leaves a
            // visible diagonal seam across otherwise flat ground. 2048 texels over 25 m is plenty.
            Tune(MobileAssetPath, shadowDistance, shadowmapResolution: 2048, cascades: 1, softShadowQuality: 2);
            Tune(PCAssetPath, shadowDistance, shadowmapResolution: 2048, cascades: 1, softShadowQuality: 3);

            AssetDatabase.SaveAssets();
            Debug.Log($"[GRAFT] Shadow distance set to {shadowDistance:0.0} m " +
                      $"(frame reaches {framing.FarGroundDistance:0.0} m).");
        }

        static void Tune(string path, float shadowDistance, int shadowmapResolution, int cascades, int softShadowQuality)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[GRAFT] Render pipeline asset missing: {path}");
                return;
            }

            var serialized = new SerializedObject(asset);
            Set(serialized, "m_ShadowDistance", p => p.floatValue = shadowDistance);
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
