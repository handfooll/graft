using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Graft.Tests
{
    /// <summary>
    /// A sweep over every ScriptableObject the game actually loads, checking that nothing it points
    /// at is missing.
    ///
    /// **Why this is worth a whole file.** Phase 3 shipped five combat materials that were created
    /// during the scene build and never reached disk. Every reference serialised as null. Nothing
    /// threw, nothing logged, the scene saved, and `ConfigureAll` exited zero - the first sign
    /// would have been a fight rendered entirely in Unity's default white, on the device.
    ///
    /// A test named after those five materials caught it. This is that test with the specificity
    /// taken out, so the *next* asset to lose a reference is caught by code that was written before
    /// it existed. That is the difference between a fix and a mechanism.
    ///
    /// The same sweep also catches the other half of `GOTCHAS.md`'s ScriptableObject trap: an asset
    /// created before a field existed keeps the serialised file's idea of that field forever, so a
    /// new reference field on an old asset is null until someone deletes and regenerates it.
    /// </summary>
    public class ShippedAssetTests
    {
        /// <summary>
        /// Fields deliberately allowed to be empty, as `Type.field`.
        ///
        /// Empty today, and that is the point - every entry here is a small hole in the sweep and
        /// should have to argue for itself in review.
        /// </summary>
        static readonly HashSet<string> AllowedEmpty = new HashSet<string>();

        [Test]
        public void NoShippedScriptableObjectPointsAtSomethingMissing()
        {
            var assets = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Settings" });
            Assert.Greater(assets.Length, 0,
                "No ScriptableObjects under Assets/Settings - run ProjectSetup.ConfigureAll.");

            var complaints = new List<string>();
            var swept = 0;

            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                {
                    continue;
                }

                // Only this project's own assets. Assets/Settings also holds URP's render pipeline
                // and renderer assets, and those are *full* of legitimately null references - the
                // adaptive probe volume debug shaders, for one, which are null unless the feature
                // is switched on. Sweeping those would mean an allow-list the length of URP's
                // internals, maintained forever, for no benefit: a null in Unity's own asset is
                // Unity's business, and a null in ours is a bug.
                var owned = asset.GetType().Namespace;
                if (owned == null || !owned.StartsWith("Graft"))
                {
                    continue;
                }

                swept++;
                Inspect(new SerializedObject(asset), asset.GetType().Name, path, complaints);
            }

            // The sweep silently passing because it swept nothing is the one way this test could
            // rot into decoration.
            Assert.GreaterOrEqual(swept, 5,
                $"Only {swept} of this project's own assets were swept. Expected at least the " +
                "camera framing, movement, character, combat, roster and spawn table assets - " +
                "either ConfigureAll has not run, or the namespace filter has stopped matching.");

            Assert.IsEmpty(complaints,
                "Shipped assets are pointing at things that do not exist:\n  " +
                string.Join("\n  ", complaints) +
                "\n\nA null here renders as nothing or as Unity's default white, silently, and " +
                "usually only on the device. See GOTCHAS.md - assets created mid-scene-build do " +
                "not reach disk, and a field added after an asset was created stays null until " +
                "the asset is deleted and regenerated.");
        }

        /// <summary>
        /// Walks every serialised property, including inside arrays and nested classes - which is
        /// where the interesting ones live. <c>EnemyRoster</c>'s materials, for instance, are two
        /// fields deep inside an array of a nested serialisable class, and a sweep that only looked
        /// at top-level fields would have sailed straight past the exact bug that prompted this.
        /// </summary>
        static void Inspect(SerializedObject serialized, string typeName, string path,
            List<string> complaints)
        {
            var property = serialized.GetIterator();
            var enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (property.objectReferenceValue != null)
                {
                    continue;
                }

                var field = $"{typeName}.{property.name}";
                if (AllowedEmpty.Contains(field))
                {
                    continue;
                }

                // Deliberately not distinguishing "never assigned" from "assigned, then deleted".
                // The two are told apart by an entity id whose comparison API has already been
                // renamed once in this editor version, and the fix is to look at the asset either
                // way - a version-fragile test that reports which flavour of null it found is a
                // worse trade than a stable one that reports the field.
                complaints.Add($"{path}: {property.propertyPath} is null.");
            }
        }

        [Test]
        public void EveryMaterialTheGameLoadsHasAShader()
        {
            // The other way a material fails silently. A material whose shader did not survive an
            // iOS build's shader stripping still exists, still loads, and draws magenta or nothing.
            var complaints = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || material.shader == null)
                {
                    complaints.Add($"{path}: no shader.");
                }
                else if (material.shader.name == "Hidden/InternalErrorShader")
                {
                    complaints.Add($"{path}: shader failed to compile.");
                }
            }

            Assert.IsEmpty(complaints, string.Join("\n  ", complaints));
        }
    }
}
