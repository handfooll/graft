using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Builds the primitive stand-in character from a <see cref="CharacterProportions"/>.
    ///
    /// Runtime rather than editor-only so the proportion sweep can rebuild it between captures -
    /// choosing a silhouette means looking at several of them through the real camera, and that
    /// cannot happen if the only way to change one is to re-run the editor scene builder.
    ///
    /// A capsule *and a sphere*, not one capsule: head size is the dominant term in how a character
    /// reads, and a plain capsule cannot express it at all.
    /// </summary>
    public static class CharacterRig
    {
        public const string BodyName = "Body";
        public const string HeadName = "Head";
        public const string CrestName = "Crest";
        public const string ChestName = "Chest";

        static readonly string[] PartNames = { BodyName, HeadName, CrestName, ChestName };

        public static void Build(GameObject root, CharacterProportions proportions, Material body, Material accent)
        {
            ClearParts(root);

            var bodyHeight = proportions.BodyHeight;
            var headDiameter = proportions.HeadDiameter;

            // Unity's capsule primitive is 2 units tall and 1 across at unit scale, so y scales by
            // half the intended height.
            AddPart(root, BodyName, PrimitiveType.Capsule,
                new Vector3(0f, bodyHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(proportions.width, bodyHeight * 0.5f, proportions.width), body);

            AddPart(root, HeadName, PrimitiveType.Sphere,
                new Vector3(0f, bodyHeight + headDiameter * 0.5f, 0f), Quaternion.identity,
                Vector3.one * headDiameter, body);

            // A crest along the top of the head. Under this camera the head is the part most nearly
            // facing the lens, so a line drawn across it is the clearest facing cue there is - and
            // it casts a directional shadow, which reads even in silhouette.
            AddPart(root, CrestName, PrimitiveType.Cube,
                new Vector3(0f, proportions.height - headDiameter * 0.14f, headDiameter * 0.16f),
                Quaternion.identity,
                new Vector3(headDiameter * 0.20f, headDiameter * 0.16f, headDiameter * 1.05f), accent);

            // A second cue at chest height, so facing survives the head being clipped by something
            // in front of it.
            AddPart(root, ChestName, PrimitiveType.Cube,
                new Vector3(0f, bodyHeight * 0.62f, proportions.width * 0.40f),
                Quaternion.Euler(0f, 45f, 0f),
                Vector3.one * (proportions.width * 0.38f), accent);

            ApplyCollider(root, proportions);
        }

        /// <summary>Sizes the CharacterController to match, so collision follows the silhouette.</summary>
        public static void ApplyCollider(GameObject root, CharacterProportions proportions)
        {
            var controller = root.GetComponent<CharacterController>();
            if (controller == null)
            {
                return;
            }

            controller.height = proportions.height;
            controller.radius = proportions.width * 0.5f;
            controller.center = new Vector3(0f, proportions.height * 0.5f, 0f);
            // Zero skin width makes a CharacterController jitter against walls; Unity's own guidance
            // is roughly 10% of the radius.
            controller.skinWidth = Mathf.Max(0.01f, controller.radius * 0.1f);
        }

        static void ClearParts(GameObject root)
        {
            foreach (var name in PartNames)
            {
                var existing = root.transform.Find(name);
                if (existing == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(existing.gameObject);
                    // Destroy is deferred to end of frame, and the replacement is created now -
                    // renaming stops Find() handing back the corpse.
                    existing.name = name + "_discarded";
                }
                else
                {
                    Object.DestroyImmediate(existing.gameObject);
                }
            }
        }

        static void AddPart(GameObject parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            // The CharacterController is the player's only collider. Leaving these on gives the
            // controller its own body to collide with.
            var collider = part.GetComponent<Collider>();
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }

            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
