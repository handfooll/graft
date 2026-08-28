using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// A health bar in world space, floating over its owner. Two quads: a dark backdrop and a fill
    /// that shrinks from the right.
    ///
    /// **World space, not screen space.** The usual objection - that a world-space bar changes size
    /// as it moves around the screen - does not apply here, because D2 fixes the camera's pitch,
    /// distance and yaw and forbids all three from changing. Under a camera that never moves
    /// relative to the ground, a fixed metre width *is* a fixed pixel width. A screen-space bar
    /// would need a world-to-screen projection every frame, per enemy, to arrive at the same place.
    ///
    /// The bar does not cast shadows. Ten enemies each throwing a small hard-edged rectangle onto
    /// the ground beside them is the whole rim-of-the-world mistake from Phase 2 in miniature.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        const string RootName = "HealthBar";

        /// <summary>
        /// How thick the bar is, in metres. It is a flattened cube rather than a quad.
        ///
        /// A quad has one face and is culled from behind, so whether it is visible at all comes
        /// down to which way Unity's quad mesh happens to point once it has been through the
        /// camera's rotation and a 180-degree correction. Getting that backwards costs nothing at
        /// build time, throws no error, and produces enemies with no health bars - which is
        /// indistinguishable from never having built one. A box has no wrong side, and at two
        /// centimetres it reads identically under a camera that cannot move.
        /// </summary>
        const float Thickness = 0.02f;

        Damageable _owner;
        Transform _root;
        Transform _fill;
        float _width;
        Quaternion _facing;
        bool _facingKnown;
        bool _alwaysVisible = true;
        bool _everDamaged;

        /// <summary>
        /// Builds the bar and starts listening. Safe to call again on a pooled enemy: the quads are
        /// reused and only the subscription and the dimensions are redone.
        /// </summary>
        public void Bind(Damageable owner, CombatTuning tuning, float ownerHeight)
        {
            _owner = owner;
            _width = tuning.healthBarWidth;
            _alwaysVisible = tuning.healthBarsAlwaysVisible;
            _everDamaged = false;

            EnsureBuilt(tuning);

            _root.localPosition = new Vector3(0f, ownerHeight + tuning.healthBarLift, 0f);
            _root.localScale = Vector3.one;

            if (_owner != null)
            {
                _owner.Damaged -= OnDamaged;
                _owner.Damaged += OnDamaged;
            }

            Redraw();
        }

        void OnDamaged(Damageable owner, float amount)
        {
            if (amount > 0f)
            {
                _everDamaged = true;
            }

            Redraw();
        }

        void OnDisable()
        {
            if (_owner != null)
            {
                _owner.Damaged -= OnDamaged;
            }
        }

        void LateUpdate()
        {
            if (_root == null)
            {
                return;
            }

            // Turned to the camera in world space, so the owner spinning to face the player does
            // not spin the bar edge-on. The camera's rotation is read once and cached - D2 fixes
            // it, and a Camera.main lookup per enemy per frame to re-learn a constant is not free.
            //
            // The root takes the camera's rotation exactly, which puts local +X along screen-right
            // and local +Z pointing away from the lens. The quads themselves are yawed 180 degrees
            // so their faces look back at it; doing the flip here instead would mirror the axis the
            // fill shrinks along and the bar would empty from the wrong end.
            if (!_facingKnown)
            {
                var camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                _facing = camera.transform.rotation;
                _facingKnown = true;
            }

            _root.rotation = _facing;
        }

        void Redraw()
        {
            if (_root == null || _owner == null)
            {
                return;
            }

            var fraction = Mathf.Clamp01(_owner.Fraction);
            var visible = _owner.IsAlive && (_alwaysVisible || _everDamaged);
            _root.gameObject.SetActive(visible);

            if (!visible)
            {
                return;
            }

            // Shrinks from the right by scaling the bar and walking it left by half of what it
            // lost, because it scales about its own centre and a bar that empties from both ends
            // reads as a countdown rather than as damage.
            //
            // Both numbers are in the holder's units, not metres - the holder is already scaled to
            // the bar's real width, which is the whole reason this is two lines. Offsetting by
            // metres here multiplies by that width a second time and the fill slides clean off the
            // end of its own backdrop.
            _fill.localScale = new Vector3(Mathf.Max(0.0001f, fraction), 1f, Thickness);
            _fill.localPosition = new Vector3(-(1f - fraction) * 0.5f, 0f, -Thickness);
        }

        void EnsureBuilt(CombatTuning tuning)
        {
            if (_root != null)
            {
                return;
            }

            var existing = transform.Find(RootName);
            if (existing != null)
            {
                _root = existing;
                // Not existing.Find("Fill") - the fill sits under the holder, and Transform.Find
                // with a bare name only looks at direct children. It would hand back null and the
                // next redraw would throw on a pooled enemy's second life.
                _fill = existing.Find("FillHolder/Fill");
                return;
            }

            _root = new GameObject(RootName).transform;
            _root.SetParent(transform, false);

            var backdrop = Slab("Backdrop", _root, tuning.healthBarBackdropMaterial);
            backdrop.localScale = new Vector3(
                tuning.healthBarWidth * 1.10f, tuning.healthBarHeight * 1.35f, Thickness);

            // The fill hangs off a holder scaled to the bar's real size, so the fill's own
            // localScale.x can be the health fraction and nothing else - which is the only reason
            // Redraw is two lines rather than six.
            var holder = new GameObject("FillHolder").transform;
            holder.SetParent(_root, false);
            holder.localScale = new Vector3(tuning.healthBarWidth, tuning.healthBarHeight, 1f);

            _fill = Slab("Fill", holder, tuning.healthBarFillMaterial);
            _fill.localScale = new Vector3(1f, 1f, Thickness);
        }

        static Transform Slab(string name, Transform parent, Material material)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, false);

            var collider = slab.GetComponent<Collider>();
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }

            var renderer = slab.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return slab.transform;
        }
    }
}
