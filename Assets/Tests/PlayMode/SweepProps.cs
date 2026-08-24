using Graft.Game;
using UnityEngine;

namespace Graft.Tests
{
    /// <summary>
    /// The throwaway scenery every tuning sweep needs. Never saved - the World scene stays exactly
    /// as the scene builder wrote it.
    ///
    /// Both sweeps show the same three things, because framing and silhouette have to be judged
    /// against the same questions: is the character readable, is a *mutation* readable, and how
    /// much warning is there before something reaches you.
    /// </summary>
    public static class SweepProps
    {
        /// <summary>Roughly "already on you" and "you have a moment".</summary>
        static readonly float[] RingRadii = { 6f, 11f };

        const int MarkersPerRing = 8;

        public static void BuildApproachMarkers(Transform player, CharacterProportions shape)
        {
            foreach (var radius in RingRadii)
            {
                var shade = radius <= 6f ? new Color(0.72f, 0.24f, 0.24f) : new Color(0.45f, 0.30f, 0.42f);
                for (var i = 0; i < MarkersPerRing; i++)
                {
                    var angle = i * Mathf.PI * 2f / MarkersPerRing;
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    marker.name = $"Marker_{radius}_{i}";
                    // Enemy-sized means player-sized. Scaling these off the player's own height is
                    // what keeps a proportion sweep honest: shrink the character against fixed
                    // markers and you are looking at a zoom change wearing a disguise.
                    marker.transform.localScale = new Vector3(shape.width, shape.height * 0.45f, shape.width);
                    marker.transform.position = player.position +
                        new Vector3(Mathf.Sin(angle) * radius, shape.height * 0.45f, Mathf.Cos(angle) * radius);
                    Paint(marker, shade);
                }
            }
        }

        /// <summary>
        /// Cubes standing in for Phase 5's socket props - a pair of head grafts and two arm grafts,
        /// at the size a real horn or claw would be. They are not the art; they are the question
        /// "would art this size be visible at all", which is the question Phase 5's gate turns on.
        /// </summary>
        public static void BuildGraftStandIns(Transform player, CharacterProportions shape)
        {
            var head = shape.HeadDiameter;
            var shoulder = shape.BodyHeight * 0.62f;
            var headCentre = shape.BodyHeight + head * 0.5f;

            AddProp(player, "Graft_HornRight", new Vector3(head * 0.34f, headCentre + head * 0.5f, 0f),
                new Vector3(head * 0.24f, head * 0.62f, head * 0.24f));
            AddProp(player, "Graft_HornLeft", new Vector3(-head * 0.34f, headCentre + head * 0.5f, 0f),
                new Vector3(head * 0.24f, head * 0.62f, head * 0.24f));
            AddProp(player, "Graft_ArmRight", new Vector3(shape.width * 0.58f, shoulder, shape.width * 0.1f),
                new Vector3(shape.width * 0.22f, shape.width * 0.22f, shape.width * 0.66f));
            AddProp(player, "Graft_ArmLeft", new Vector3(-shape.width * 0.58f, shoulder, shape.width * 0.1f),
                new Vector3(shape.width * 0.22f, shape.width * 0.22f, shape.width * 0.66f));
        }

        static void AddProp(Transform player, string name, Vector3 localPosition, Vector3 scale)
        {
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = name;
            prop.transform.SetParent(player, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = scale;
            Object.Destroy(prop.GetComponent<Collider>());
            Paint(prop, new Color(0.36f, 0.72f, 0.42f));
        }

        public static void Paint(GameObject target, Color colour)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            target.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public static void DestroyByPrefix(string prefix)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith(prefix))
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }
        }
    }
}
