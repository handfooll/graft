using System;
using Graft.Core;
using UnityEngine;

namespace Graft.Editor
{
    /// <summary>
    /// The scatter props that give each biome its silhouette.
    ///
    /// Colour alone does not carry a place. Two areas can be told apart by ground colour in a
    /// screenshot, but the thing that makes one *memorable* is what stands up out of it - and
    /// under a 47.5-degree camera, what stands up also casts the long shadow that gives the ground
    /// its depth. So each area gets a shape family as well as a palette: rounded here, spiked
    /// there, monumental somewhere else.
    ///
    /// Primitives only, until Phase 6 (DESIGN.md). Flat decorations deliberately keep no collider:
    /// a 0.35 m plate with one is a wall to a CharacterController whose step offset is 0.3, and it
    /// would punch a hole in the NavMesh for something the player should walk straight over.
    /// </summary>
    public static class AreaProps
    {
        public static GameObject Create(Area area, Transform parent, Vector3 position,
            System.Random rng, Material body, Material accent, Material shade)
        {
            var root = new GameObject($"{area}_Prop");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));

            switch (area)
            {
                case Area.Verge: Verge(root, rng, body, accent); break;
                case Area.RustFlats: RustFlats(root, rng, body, accent, shade); break;
                case Area.GlassMire: GlassMire(root, rng, body, accent, shade); break;
                case Area.BoneReach: BoneReach(root, rng, body, accent); break;
                case Area.EmberScar: EmberScar(root, rng, body, accent, shade); break;
                default: throw new ArgumentOutOfRangeException(nameof(area), area, "Base is built, not scattered.");
            }

            MarkStatic(root);
            return root;
        }

        /// <summary>Rounded and low. The safest place looks like nothing is trying to cut you.</summary>
        static void Verge(GameObject root, System.Random rng, Material body, Material accent)
        {
            if (rng.NextDouble() < 0.6)
            {
                var width = Range(rng, 1.6f, 2.9f);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, Range(rng, 0.15f, 0.45f), 0f),
                    Quaternion.Euler(Range(rng, -14f, 14f), 0f, Range(rng, -14f, 14f)),
                    new Vector3(width, Range(rng, 0.8f, 1.35f), width * Range(rng, 0.75f, 1.1f)), body);
                return;
            }

            var height = Range(rng, 0.7f, 1.3f);
            var radius = Range(rng, 0.45f, 0.8f);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, height * 0.5f, 0f), Quaternion.identity,
                new Vector3(radius, height * 0.5f, radius), body);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, height + 0.04f, 0f), Quaternion.identity,
                new Vector3(radius * 0.95f, 0.05f, radius * 0.95f), accent, collide: false);
        }

        /// <summary>
        /// Leaning spires and cracked plates. Mostly spires: the vertical is the signature, and
        /// the first pass - half plates, at four metres across, in the bright accent colour -
        /// buried it under a floor of orange rectangles.
        /// </summary>
        static void RustFlats(GameObject root, System.Random rng, Material body, Material accent, Material shade)
        {
            if (rng.NextDouble() < 0.76)
            {
                var height = Range(rng, 3.2f, 5.2f);
                var lean = Quaternion.Euler(Range(rng, 6f, 19f), 0f, Range(rng, -8f, 8f));
                var width = Range(rng, 0.4f, 0.65f);
                Part(root, PrimitiveType.Cube, lean * new Vector3(0f, height * 0.5f, 0f), lean,
                    new Vector3(width, height, width * Range(rng, 0.7f, 1.3f)), body);
                Part(root, PrimitiveType.Cube, lean * new Vector3(0f, height, 0f), lean,
                    new Vector3(width * 1.5f, 0.35f, width * 1.5f), accent, collide: false);
                return;
            }

            var plate = Range(rng, 1.6f, 3f);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f),
                Quaternion.Euler(0f, Range(rng, 0f, 90f), 0f),
                new Vector3(plate, 0.22f, plate * Range(rng, 0.6f, 1f)), shade, collide: false);
        }

        /// <summary>
        /// Shard clusters and still pools. Everything here is either sharp or wet - and the pools
        /// are the darkest thing in the area, because water that is lighter than the bank it sits
        /// in reads as a hole cut in the floor rather than as depth.
        /// </summary>
        static void GlassMire(GameObject root, System.Random rng, Material body, Material accent, Material shade)
        {
            if (rng.NextDouble() < 0.7)
            {
                var shards = 2 + rng.Next(2);
                for (var i = 0; i < shards; i++)
                {
                    var height = Range(rng, 2.2f, 4.2f);
                    var tilt = Quaternion.Euler(Range(rng, -34f, 34f), Range(rng, 0f, 360f), Range(rng, -34f, 34f));
                    var offset = new Vector3(Range(rng, -1.1f, 1.1f), 0f, Range(rng, -1.1f, 1.1f));
                    Part(root, PrimitiveType.Cube, offset + tilt * new Vector3(0f, height * 0.42f, 0f), tilt,
                        new Vector3(Range(rng, 0.35f, 0.6f), height, Range(rng, 0.35f, 0.6f)),
                        i == 0 ? accent : body);
                }

                return;
            }

            var pool = Range(rng, 2.4f, 4.6f);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), Quaternion.identity,
                new Vector3(pool, 0.03f, pool * Range(rng, 0.7f, 1f)), shade, collide: false);
        }

        /// <summary>Big, pale, sparse. Arches you walk under, not rocks you walk round.</summary>
        static void BoneReach(GameObject root, System.Random rng, Material body, Material accent)
        {
            if (rng.NextDouble() < 0.5)
            {
                var height = Range(rng, 3.6f, 5.4f);
                var spread = Range(rng, 1.4f, 2.4f);
                for (var side = -1; side <= 1; side += 2)
                {
                    var lean = Quaternion.Euler(0f, 0f, side * Range(rng, 16f, 26f));
                    Part(root, PrimitiveType.Cube,
                        new Vector3(side * spread, 0f, 0f) + lean * new Vector3(0f, height * 0.5f, 0f), lean,
                        new Vector3(Range(rng, 0.35f, 0.55f), height, Range(rng, 0.5f, 0.8f)), body);
                }

                return;
            }

            var segments = 3 + rng.Next(2);
            for (var i = 0; i < segments; i++)
            {
                var scale = Range(rng, 1.1f, 1.7f) * (1f - i * 0.12f);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.3f + i * 0.55f, i * Range(rng, 0.7f, 1.1f)),
                    Quaternion.identity, new Vector3(scale, scale * 0.55f, scale * 0.8f),
                    i % 2 == 0 ? body : accent);
            }
        }

        /// <summary>
        /// Black monoliths, glowing vents and cracks in the ground. The only area in the world
        /// that emits light, and the only one whose ground is broken open.
        ///
        /// The seam on a monolith is duplicated on both faces because the scatter yaws each prop
        /// at random: with one seam, half of them face away from the camera and the area's whole
        /// signature is invisible from wherever the player happens to be standing.
        /// </summary>
        static void EmberScar(GameObject root, System.Random rng, Material body, Material accent, Material shade)
        {
            var roll = rng.NextDouble();

            if (roll < 0.44)
            {
                var height = Range(rng, 2.8f, 5f);
                var width = Range(rng, 1f, 1.8f);
                var depth = width * Range(rng, 0.5f, 0.9f);
                var tilt = Quaternion.Euler(Range(rng, -9f, 9f), 0f, Range(rng, -9f, 9f));
                Part(root, PrimitiveType.Cube, tilt * new Vector3(0f, height * 0.5f, 0f), tilt,
                    new Vector3(width, height, depth), body);

                for (var face = -1; face <= 1; face += 2)
                {
                    Part(root, PrimitiveType.Cube,
                        tilt * new Vector3(0f, height * 0.45f, face * (depth * 0.5f + 0.06f)), tilt,
                        new Vector3(width * 0.22f, height * 0.7f, 0.12f), accent, collide: false);
                }

                return;
            }

            if (roll < 0.72)
            {
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f),
                    Quaternion.Euler(0f, Range(rng, 0f, 90f), 0f),
                    new Vector3(Range(rng, 1f, 2f), 0.16f, Range(rng, 1f, 2f)), accent, collide: false);
                var rocks = 3 + rng.Next(3);
                for (var i = 0; i < rocks; i++)
                {
                    var angle = i / (float)rocks * 360f + Range(rng, -20f, 20f);
                    var radius = Range(rng, 1.2f, 2.1f);
                    var size = Range(rng, 0.5f, 1.1f);
                    Part(root, PrimitiveType.Cube,
                        new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, size * 0.35f,
                            Mathf.Cos(angle * Mathf.Deg2Rad) * radius),
                        Quaternion.Euler(Range(rng, -20f, 20f), angle, Range(rng, -20f, 20f)),
                        new Vector3(size, size * 0.7f, size), shade, collide: false);
                }

                return;
            }

            // A crack running through the ground. Flat, so it never blocks anything, and lit from
            // inside, so it is the one thing in this area that is visible in shadow.
            var segments = 2 + rng.Next(3);
            var run = Vector3.zero;
            var bearing = Range(rng, 0f, 360f);
            for (var i = 0; i < segments; i++)
            {
                var length = Range(rng, 1.6f, 3.2f);
                bearing += Range(rng, -38f, 38f);
                var step = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * length;
                Part(root, PrimitiveType.Cube, run + step * 0.5f + new Vector3(0f, 0.03f, 0f),
                    Quaternion.Euler(0f, bearing, 0f),
                    new Vector3(Range(rng, 0.18f, 0.34f), 0.06f, length), accent, collide: false);
                run += step;
            }
        }

        static void Part(GameObject parent, PrimitiveType primitive, Vector3 localPosition,
            Quaternion localRotation, Vector3 localScale, Material material, bool collide = true)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = primitive.ToString();
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (!collide)
            {
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            }
        }

        static void MarkStatic(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.isStatic = true;
            }
        }

        static float Range(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);
    }
}
