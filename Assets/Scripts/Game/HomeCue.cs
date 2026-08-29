using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Chevrons on the ground beside the player, pointing at Base, shown whenever there is
    /// something to lose. BUILD_PLAN.md Phase 4: "a visible directional cue pointing home".
    ///
    /// **World space, not a screen-edge compass** - the same argument that put the health bars in
    /// the world. D2 fixes the camera's pitch, distance and yaw, so a metre on the ground is a
    /// fixed number of pixels and an arrow drawn flat on the floor lands where it should without a
    /// projection per frame. It also stays *in* the world the player is looking at, rather than
    /// asking them to glance at a border and translate.
    ///
    /// It appears only while carrying. That is the point: the cue is not navigation, it is the
    /// extraction decision made visible. Nothing to lose, nothing to point at.
    /// </summary>
    public class HomeCue : MonoBehaviour
    {
        /// <summary>Height above the ground. Enough to clear z-fighting with the floor tiles.</summary>
        const float GroundClearance = 0.05f;

        /// <summary>Chevron arm length and thickness, in metres, before the urgency scale.</summary>
        const float ArmLength = 0.42f;
        const float ArmThickness = 0.085f;

        [SerializeField] EssenceTuning tuning;
        [SerializeField] WorldRegions regions;
        [SerializeField] PlayerCargo cargo;

        Transform _root;
        Transform[] _chevrons;
        Vector3 _home;
        bool _homeKnown;

        public EssenceTuning Tuning { get => tuning; set => tuning = value; }
        public WorldRegions Regions { get => regions; set => regions = value; }
        public PlayerCargo Cargo { get => cargo; set => cargo = value; }

        /// <summary>Drawn right now. The gate's first image asserts this is false.</summary>
        public bool IsShowing => _root != null && _root.gameObject.activeSelf;

        /// <summary>The bearing the cue is pointing, in degrees. Asserted by the tour.</summary>
        public float Heading => _root != null ? _root.eulerAngles.y : 0f;

        void LateUpdate()
        {
            if (tuning == null || regions == null || cargo == null)
            {
                return;
            }

            var ledger = cargo.Ledger;
            EnsureBuilt();

            if (!_homeKnown)
            {
                _home = regions.AnchorFor(Area.Base);
                _homeKnown = true;
            }

            var here = cargo.transform.position;
            var toHome = _home - here;
            toHome.y = 0f;

            // Hidden when there is nothing at stake, and hidden once home is close enough to see -
            // an arrow pointing at something already on screen is clutter, and clutter is what the
            // HUD budget note in STATUS.md exists to refuse.
            var carrying = ledger != null && ledger.Carried.Carried > 0f;
            var show = carrying && toHome.magnitude > tuning.homeCueFadeDistance;

            _root.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            _root.position = here + Vector3.up * GroundClearance;
            _root.rotation = Quaternion.LookRotation(toHome.normalized, Vector3.up);

            // Urgency is spacing and size, not colour. A cue that changed hue as the bar filled
            // would be a second colour language on a screen that already uses red for health, and
            // it would say nothing at all to the roughly one player in twelve who cannot separate
            // the two ends of it.
            var urgency = ledger != null ? Mathf.Clamp01(ledger.Carried.Fraction) : 0f;
            var scale = Mathf.Lerp(0.8f, 1.35f, urgency);
            var spacing = Mathf.Lerp(0.55f, 0.34f, urgency);

            for (var i = 0; i < _chevrons.Length; i++)
            {
                var chevron = _chevrons[i];
                chevron.localPosition = new Vector3(0f, 0f, tuning.homeCueRadius + i * spacing);
                chevron.localScale = Vector3.one * scale;
            }
        }

        void EnsureBuilt()
        {
            var wanted = Mathf.Max(1, tuning.homeCueChevrons);
            if (_root != null && _chevrons != null && _chevrons.Length == wanted)
            {
                return;
            }

            if (_root != null)
            {
                Destroy(_root.gameObject);
            }

            // Parented to the scene rather than to the player, because the player rotates to face
            // the way they are walking and the cue must not turn with them - it points at Base,
            // which is a place, not a direction relative to the character.
            _root = new GameObject("HomeCue").transform;
            _chevrons = new Transform[wanted];

            for (var i = 0; i < wanted; i++)
            {
                var chevron = new GameObject($"Chevron{i}").transform;
                chevron.SetParent(_root, false);
                Arm(chevron, -35f);
                Arm(chevron, 35f);
                _chevrons[i] = chevron;
            }

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// One half of a chevron: a flattened box swung out from the point. Two of them make a "&gt;"
        /// lying on the floor, which reads as a direction from any distance the camera can be at.
        /// </summary>
        void Arm(Transform parent, float yaw)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Arm";
            slab.transform.SetParent(parent, false);
            Destroy(slab.GetComponent<Collider>());

            slab.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            slab.transform.localPosition = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -ArmLength * 0.5f);
            slab.transform.localScale = new Vector3(ArmThickness, 0.02f, ArmLength);

            var renderer = slab.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = tuning.homeCueMaterial;
            // Flat on the floor and emissive: a cue that cast a shadow would draw a second chevron
            // beside itself under a 38-degree sun, and one that took the sun's light would vanish
            // into Ember Scar's near-black ground exactly where it matters most.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }
        }
    }
}
