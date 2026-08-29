using Graft.Core;
using Graft.Game;
using UnityEngine;

namespace Graft.Editor
{
    /// <summary>
    /// The Anchor: the safe hub at the mouth of the world (DESIGN.md §7).
    ///
    /// Every biome is scattered; Base is the only place that is *arranged*. Right angles, a
    /// symmetric ring, a paved cross, a monument at the centre - under a top-down camera that
    /// reads as "someone built this" before you have read a single word, which is the whole job of
    /// the Phase 2 gate image for this area.
    ///
    /// It is also composed for the frame it will be seen in. The camera shows 9.5 m across, so
    /// anything past about 4.5 m either side of the player is off screen: the inner ring sits at
    /// 4.6 m and the monument 3.5 m ahead of the spawn point, deliberately, so the picture is full
    /// rather than being a wide plaza glimpsed through a keyhole.
    /// </summary>
    public static class BaseBuilder
    {
        /// <summary>Named so the screenshot tour can drive into it - it is the only wall in Base.</summary>
        public const string MonolithName = "Base_Monolith";

        public static GameObject Build(Transform parent, WorldLayout layout, Vector3 centre)
        {
            var palette = layout.PaletteFor(Area.Base);
            var body = MaterialLibrary.GetOrCreate("M_Prop_Base", palette.prop);
            var accent = MaterialLibrary.GetOrCreate("M_PropAccent_Base", palette.propAccent, palette.accentEmissive);
            var paving = MaterialLibrary.GetOrCreate("M_Base_Paving", palette.ground * 1.3f);
            var shade = MaterialLibrary.GetOrCreate("M_PropShade_Base", palette.propShade);

            var root = new GameObject("Base");
            root.transform.SetParent(parent, false);
            // Placed from the map, not at the world origin. Every block below is positioned in
            // Base's local space, so if Base ever moves - and it does, between the two layouts -
            // the plaza has to move with the ground disc rather than staying behind at (0,0).
            root.transform.localPosition = centre;

            // The bank. Tall enough to be the thing your eye lands on from anywhere in the plaza,
            // and the only lit object in a world that is otherwise lit only by the sun.
            var monolith = Block(root, MonolithName, new Vector3(0f, 2.1f, 3.5f),
                Quaternion.identity, new Vector3(2.4f, 4.2f, 1.8f), body);
            Block(root, "Base_BankLight", new Vector3(0f, 3.2f, 2.55f), Quaternion.identity,
                new Vector3(1.5f, 1.3f, 0.16f), accent, collide: false);
            Block(root, "Base_BankLight_Back", new Vector3(0f, 3.2f, 4.45f), Quaternion.identity,
                new Vector3(1.5f, 1.3f, 0.16f), accent, collide: false);

            PavingStrip(root, "Base_Paving_NS", new Vector3(3.4f, 0.06f, layout.baseRadius * 1.7f), paving);
            PavingStrip(root, "Base_Paving_EW", new Vector3(layout.baseRadius * 1.7f, 0.06f, 3.4f), paving);

            // Kept shorter than they first were. At 3.1 m and 4.2 m the ring towered over a 1.5 m
            // character and the plaza read as a canyon; the point of the ring is order, not scale.
            Ring(root, "Base_InnerPillar", count: 4, radius: 5.6f, startAngle: 45f,
                size: new Vector3(0.62f, 2f, 0.62f), body, accent);
            Ring(root, "Base_OuterPillar", count: 8, radius: 12f, startAngle: 22.5f,
                size: new Vector3(0.85f, 3f, 0.85f), body, accent);

            // The kerb. No collider on purpose: it marks where Base ends, and a knee-high ring you
            // have to find a gap in would turn the safest place in the game into an obstacle course.
            var kerbRadius = layout.baseRadius - 1.8f;
            for (var i = 0; i < 28; i++)
            {
                var angle = i / 28f * 360f;
                Block(root, "Base_Kerb", Polar(angle, kerbRadius, 0.2f),
                    Quaternion.Euler(0f, angle, 0f), new Vector3(2.6f, 0.4f, 0.9f),
                    i % 7 == 0 ? accent : shade, collide: false);
            }

            // The **bank plaza**: the inner of Base's two rings (D-P4.2). Crossing this line banks
            // the cargo; the outer ring is only safe.
            //
            // It has to be a line you can see, because it is the only boundary in the game that
            // does something the moment you cross it - and because the whole reason there are two
            // rings is that a player fleeing home should be able to stop *outside* it and keep
            // their expedition. A rule you can only discover by triggering it is not a retreat
            // valve. Dashed, low and uncollidable, so it reads as painted ground, not a fence.
            //
            // **Essence gold, not Base's cyan.** Every other lit thing in the plaza is cyan, so a
            // cyan ring reads as more of the same decoration rather than as the one line in the
            // game that does something when you step over it. Gold is the colour of essence, of a
            // full cargo bar and of the cue that points here - which makes this the place where the
            // gold you are carrying turns into gold you have kept, said in the only language a
            // top-down game has room for.
            BankRing(root, layout.bankRadius,
                MaterialLibrary.GetOrCreate("M_Base_BankRing", new Color(1.00f, 0.72f, 0.26f), true));

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.isStatic = true;
            }

            monolith.name = MonolithName;
            return root;
        }

        /// <summary>
        /// The dashed, lit ring marking where banking happens.
        ///
        /// The dash count is derived from the circumference rather than fixed, so changing
        /// <c>bankRadius</c> moves the line without also changing how a dash looks - a fixed count
        /// on a bigger circle stretches every segment and the ring stops reading as a marking.
        /// </summary>
        static void BankRing(GameObject parent, float radius, Material accent)
        {
            const float DashLength = 1.5f;
            const float DashGap = 0.85f;

            var circumference = 2f * Mathf.PI * Mathf.Max(0.5f, radius);
            var count = Mathf.Max(8, Mathf.RoundToInt(circumference / (DashLength + DashGap)));

            for (var i = 0; i < count; i++)
            {
                var angle = i / (float)count * 360f;
                Block(parent, "Base_BankRing", Polar(angle, radius, 0.05f),
                    Quaternion.Euler(0f, angle, 0f), new Vector3(DashLength, 0.1f, 0.34f),
                    accent, collide: false);
            }
        }

        static void Ring(GameObject parent, string name, int count, float radius, float startAngle,
            Vector3 size, Material body, Material accent)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = startAngle + i / (float)count * 360f;
                Block(parent, name, Polar(angle, radius, size.y * 0.5f),
                    Quaternion.Euler(0f, angle, 0f), size, body);
                Block(parent, name + "_Cap", Polar(angle, radius, size.y + 0.12f),
                    Quaternion.Euler(0f, angle, 0f),
                    new Vector3(size.x * 1.35f, 0.24f, size.z * 1.35f), accent, collide: false);
            }
        }

        static void PavingStrip(GameObject parent, string name, Vector3 size, Material material) =>
            Block(parent, name, new Vector3(0f, 0.03f, 0f), Quaternion.identity, size, material, collide: false);

        static Vector3 Polar(float degrees, float radius, float height)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * radius, height, Mathf.Cos(radians) * radius);
        }

        static GameObject Block(GameObject parent, string name, Vector3 position, Quaternion rotation,
            Vector3 scale, Material material, bool collide = true)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent.transform, false);
            block.transform.localPosition = position;
            block.transform.localRotation = rotation;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (!collide)
            {
                Object.DestroyImmediate(block.GetComponent<Collider>());
            }

            return block;
        }
    }
}
