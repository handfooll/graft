using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Builds one enemy's primitive body from its <see cref="EnemyProfile"/>, the way
    /// <see cref="CharacterRig"/> builds the player's.
    ///
    /// **Four shapes, not four colours.** Colour-coding is the obvious way to tell families apart
    /// and it fails exactly where it matters: acid-green Venom on the Verge's moss green, tan Hide
    /// on Bone Reach's bleached ground. Phase 2 hit the same wall with biomes and answered it the
    /// same way - a shape family per area, because what stands up out of the ground is what the
    /// camera can actually see.
    ///
    /// What each family reads as from directly above, which is the only view this game has:
    ///
    /// | Family | Footprint | Height | The tell |
    /// |---|---|---|---|
    /// | Venom | small, narrow | lowest | a scuttling wedge with two forward spikes |
    /// | Impact | big square | tall | blocks, and two fists out front |
    /// | Hide | widest, round | low | a dome with plates over it - a shell |
    /// | Spark | smallest, round | tallest | a thin stalk, and the only thing that glows |
    ///
    /// Footprint and height also decide shadow length, and under a 38-degree sun that is a second
    /// copy of the silhouette lying on the ground beside it.
    /// </summary>
    public static class EnemyRig
    {
        /// <summary>Everything built here goes under one child, so rebuilding is one destroy.</summary>
        public const string PartsName = "Parts";

        /// <summary>
        /// Rebuilds <paramref name="root"/>'s body. Returns the parts parent, so callers that want
        /// the renderers - <see cref="HitFlash"/> - do not have to search the whole object.
        /// </summary>
        public static Transform Build(GameObject root, EnemyProfile profile)
        {
            Clear(root);

            var parts = new GameObject(PartsName);
            parts.transform.SetParent(root.transform, false);

            var body = profile.bodyMaterial;
            var accent = profile.accentMaterial;

            switch (profile.family)
            {
                case Core.EnemyFamily.Impact:
                    BuildImpact(parts, profile, body, accent);
                    break;
                case Core.EnemyFamily.Hide:
                    BuildHide(parts, profile, body, accent);
                    break;
                case Core.EnemyFamily.Spark:
                    BuildSpark(parts, profile, body, accent);
                    break;
                default:
                    BuildVenom(parts, profile, body, accent);
                    break;
            }

            return parts.transform;
        }

        public static void Clear(GameObject root)
        {
            var existing = root.transform.Find(PartsName);
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(existing.gameObject);
                // Destroy is deferred to the end of the frame and the replacement is made now, so
                // the corpse is renamed out of the way or Find() hands it straight back. The same
                // trap, and the same answer, as CharacterRig.
                existing.name = PartsName + "_discarded";
            }
            else
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        /// <summary>Low, narrow and pointed forwards. The one you are meant to ignore singly.</summary>
        static void BuildVenom(GameObject parent, EnemyProfile p, Material body, Material accent)
        {
            var h = p.height;
            var w = p.width;

            Add(parent, "Abdomen", PrimitiveType.Sphere,
                new Vector3(0f, h * 0.44f, -w * 0.14f), Quaternion.identity,
                new Vector3(w, h * 0.78f, w * 1.20f), body);

            Add(parent, "Head", PrimitiveType.Sphere,
                new Vector3(0f, h * 0.52f, w * 0.46f), Quaternion.identity,
                Vector3.one * (w * 0.52f), body);

            // Two forward spikes. On a body this small they are most of what says which way it is
            // coming at you, and they are the only part tall enough to catch the sun.
            for (var side = -1; side <= 1; side += 2)
            {
                Add(parent, side < 0 ? "FangLeft" : "FangRight", PrimitiveType.Cube,
                    new Vector3(side * w * 0.22f, h * 0.60f, w * 0.62f),
                    Quaternion.Euler(18f, side * 14f, 0f),
                    new Vector3(w * 0.11f, w * 0.11f, w * 0.58f), accent);
            }
        }

        /// <summary>Square, heavy and slow. Everything about it is blocks.</summary>
        static void BuildImpact(GameObject parent, EnemyProfile p, Material body, Material accent)
        {
            var h = p.height;
            var w = p.width;

            Add(parent, "Torso", PrimitiveType.Cube,
                new Vector3(0f, h * 0.46f, 0f), Quaternion.identity,
                new Vector3(w * 0.86f, h * 0.72f, w * 0.72f), body);

            Add(parent, "Head", PrimitiveType.Cube,
                new Vector3(0f, h * 0.90f, w * 0.04f), Quaternion.identity,
                new Vector3(w * 0.46f, h * 0.20f, w * 0.46f), body);

            // Fists held out in front. They are the attack, and holding them forward means the
            // silhouette announces the reach before the swing does.
            for (var side = -1; side <= 1; side += 2)
            {
                Add(parent, side < 0 ? "FistLeft" : "FistRight", PrimitiveType.Cube,
                    new Vector3(side * w * 0.56f, h * 0.52f, w * 0.26f),
                    Quaternion.Euler(0f, side * 12f, 0f),
                    Vector3.one * (w * 0.40f), accent);
            }
        }

        /// <summary>Wide, round and low - a shell. The one that will not die.</summary>
        static void BuildHide(GameObject parent, EnemyProfile p, Material body, Material accent)
        {
            var h = p.height;
            var w = p.width;

            Add(parent, "Shell", PrimitiveType.Sphere,
                new Vector3(0f, h * 0.42f, 0f), Quaternion.identity,
                new Vector3(w, h * 1.05f, w * 0.92f), body);

            // Three plates across the shell, front to back. Concentric bands read as armour from
            // above in a way a flat colour never does, and they break the dome up so it is not
            // mistaken for a boulder - Bone Reach is full of pale rounded rock.
            for (var i = 0; i < 3; i++)
            {
                var z = (i - 1) * w * 0.26f;
                var span = Mathf.Lerp(0.86f, 0.62f, Mathf.Abs(i - 1));
                Add(parent, "Plate" + i, PrimitiveType.Cube,
                    new Vector3(0f, h * 0.74f - Mathf.Abs(i - 1) * h * 0.10f, z), Quaternion.identity,
                    new Vector3(w * span, h * 0.10f, w * 0.13f), accent);
            }

            // A blunt head at the front, because a symmetrical dome has no facing at all, and a
            // Shellback that has turned to face you should look like it has.
            Add(parent, "Head", PrimitiveType.Sphere,
                new Vector3(0f, h * 0.34f, w * 0.46f), Quaternion.identity,
                Vector3.one * (w * 0.30f), body);
        }

        /// <summary>Thin, tall and lit. The only family that glows, and the only one that shoots.</summary>
        static void BuildSpark(GameObject parent, EnemyProfile p, Material body, Material accent)
        {
            var h = p.height;
            var w = p.width;

            // Unity's capsule is 2 units tall at unit scale, so y scales by half the span.
            Add(parent, "Stalk", PrimitiveType.Capsule,
                new Vector3(0f, h * 0.34f, 0f), Quaternion.identity,
                new Vector3(w, h * 0.34f, w), body);

            // The lantern, held high on purpose. This is the family that can hurt you from outside
            // your own reach, so it should be the thing your eye lands on first - and a glowing
            // head on the tallest silhouette in the game is that.
            Add(parent, "Lantern", PrimitiveType.Sphere,
                new Vector3(0f, h * 0.84f, 0f), Quaternion.identity,
                Vector3.one * (w * 0.92f), accent);

            Add(parent, "Muzzle", PrimitiveType.Cube,
                new Vector3(0f, h * 0.84f, w * 0.56f), Quaternion.identity,
                new Vector3(w * 0.16f, w * 0.16f, w * 0.42f), accent);
        }

        static void Add(GameObject parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            // The single capsule collider EnemyAgent puts on the root is the enemy's whole physical
            // presence. Primitive colliders left on here would be a second, differently shaped body
            // that the NavMeshAgent does not know about.
            var collider = part.GetComponent<Collider>();
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }

            if (material != null)
            {
                part.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
