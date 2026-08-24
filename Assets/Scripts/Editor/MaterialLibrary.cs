using System.IO;
using UnityEditor;
using UnityEngine;

namespace Graft.Editor
{
    /// <summary>
    /// Creates the flat URP materials the primitive phases run on. Phase 6 swaps these for art;
    /// until then every colour in the project comes from here so palettes stay adjustable.
    /// </summary>
    public static class MaterialLibrary
    {
        public const string Folder = "Assets/Materials";

        public static Material GetOrCreate(string name, Color colour)
        {
            var path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                Apply(existing, colour);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Directory.CreateDirectory(Folder);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    "URP Lit shader not found - the Universal Render Pipeline package did not import.");
            }

            var material = new Material(shader) { name = name };
            Apply(material, colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Flat and matte, with specular highlights switched off entirely.
        ///
        /// URP's default specular lobe is wide at low smoothness, and across a floor the size of a
        /// biome that reads as a large soft gradient - a diagonal wash that looks like a lighting
        /// bug or a terrain seam. Nothing in the primitive phases wants a highlight, and the Phase 6
        /// target is toon shading, which does not want one either.
        /// </summary>
        static void Apply(Material material, Color colour)
        {
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }
    }
}
