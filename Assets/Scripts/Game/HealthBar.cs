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

            // Shrinks from the right by scaling the quad and walking it left by half of what it
            // lost, because a quad scales about its centre and a bar that empties from both ends
            // reads as a countdown rather than as damage.
            _fill.localScale = new Vector3(Mathf.Max(0.0001f, fraction), 1f, 1f);
            _fill.localPosition = new Vector3(-(1f - fraction) * _width * 0.5f, 0f, -0.01f);
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
                _fill = existing.Find("Fill");
                return;
            }

            _root = new GameObject(RootName).transform;
            _root.SetParent(transform, false);

            var backdrop = Quad("Backdrop", _root, tuning.healthBarBackdropMaterial);
            backdrop.localScale = new Vector3(
                tuning.healthBarWidth * 1.10f, tuning.healthBarHeight * 1.35f, 1f);

            // The fill is a child of a holder scaled to the bar's real size, so the fill's own
            // localScale.x can be the health fraction and nothing else - which is the only reason
            // Redraw is two lines instead of six.
            var holder = new GameObject("FillHolder").transform;
            holder.SetParent(_root, false);
            holder.localScale = new Vector3(tuning.healthBarWidth, tuning.healthBarHeight, 1f);

            _fill = Quad("Fill", holder, tuning.healthBarFillMaterial);
            _fill.localScale = Vector3.one;
        }

        static Transform Quad(string name, Transform parent, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            // Unity's quad faces +Z, and the root's +Z points away from the camera. Without this
            // the bar is drawn facing into the world and backface culling makes it invisible -
            // which looks exactly like a bar that was never created.
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var collider = quad.GetComponent<Collider>();
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return quad.transform;
        }
    }
}
