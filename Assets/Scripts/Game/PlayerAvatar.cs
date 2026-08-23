using System.Collections.Generic;
using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// The hero's body on screen (GDD pillar 2: power and horror grow together).
    /// Each occupied slot grows a visible appendage tinted by the element that filled it,
    /// and rising corruption swells and reddens the whole silhouette -- so a glance at the
    /// hero tells you both what your build is and how far you have pushed it.
    /// </summary>
    public sealed class PlayerAvatar : MonoBehaviour
    {
        readonly Dictionary<GraftSlot, SpriteRenderer> _appendages = new Dictionary<GraftSlot, SpriteRenderer>();

        SpriteRenderer _body;
        SpriteRenderer _head;
        SpriteRenderer _aura;
        Transform _rig;
        VesselDefinition _vessel;
        float _corruption;
        float _displayedCorruption;
        float _facing = 1f;

        /// <summary>Where each slot's appendage sits, in local units on a 1-unit body.</summary>
        static readonly Dictionary<GraftSlot, Vector2> SlotAnchors = new Dictionary<GraftSlot, Vector2>
        {
            { GraftSlot.Head,     new Vector2( 0.00f,  0.42f) },
            { GraftSlot.Core,     new Vector2( 0.00f,  0.02f) },
            { GraftSlot.LeftArm,  new Vector2(-0.36f,  0.14f) },
            { GraftSlot.RightArm, new Vector2( 0.36f,  0.14f) },
            { GraftSlot.Legs,     new Vector2( 0.00f, -0.38f) }
        };

        public void Build(VesselDefinition vessel)
        {
            _vessel = vessel;

            _rig = new GameObject("Rig").transform;
            _rig.SetParent(transform, false);

            // The aura sits behind everything and only becomes visible as you corrupt.
            _aura = SpriteFactory.NewRenderer("Aura", _rig, SpriteFactory.Glow(),
                new Color(1f, 1f, 1f, 0f), order: 8);
            _aura.transform.localScale = Vector3.one * 2.4f;

            _body = SpriteFactory.NewRenderer("Body", _rig, SpriteFactory.Capsule(),
                Palette.HeroClean, order: 10);
            _body.transform.localScale = new Vector3(vessel.SilhouetteWidth, vessel.SilhouetteHeight, 1f);

            _head = SpriteFactory.NewRenderer("Head", _rig, SpriteFactory.Circle(),
                Palette.HeroClean, order: 11);
            _head.transform.localPosition = new Vector3(0f, 0.40f, 0f);
            _head.transform.localScale = Vector3.one * 0.42f;

            ApplyCorruptionVisuals(0f, instant: true);
        }

        /// <summary>Called whenever a slot changes. Grows the appendage in place.</summary>
        public void SetGraft(GraftSlot slot, GraftInstance graft)
        {
            if (graft == null)
            {
                if (_appendages.TryGetValue(slot, out var stale) && stale != null)
                {
                    Destroy(stale.gameObject);
                    _appendages.Remove(slot);
                }
                return;
            }

            if (!_appendages.TryGetValue(slot, out var sr) || sr == null)
            {
                sr = SpriteFactory.NewRenderer("Graft_" + slot, _rig,
                    SpriteFactory.Spiked(6 + (int)slot), Color.white, order: 12);
                sr.transform.localPosition = SlotAnchors[slot];
                _appendages[slot] = sr;
            }

            var color = Palette.For(graft.Source.Element);
            sr.color = color;

            // Reinforcing the same graft visibly thickens it, so tier reads on the body.
            float size = 0.26f + 0.07f * Mathf.Min(graft.Tier, 4);
            if (slot == GraftSlot.Core) size *= 1.25f;
            sr.transform.localScale = Vector3.one * size;

            StartCoroutine(PopIn(sr.transform, size));
        }

        System.Collections.IEnumerator PopIn(Transform t, float target)
        {
            float elapsed = 0f;
            const float dur = 0.22f;
            while (elapsed < dur && t != null)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / dur);
                // Overshoot then settle, so a new graft is impossible to miss.
                float s = target * (1f + 0.55f * Mathf.Sin(k * Mathf.PI));
                t.localScale = Vector3.one * s;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one * target;
        }

        public void SetCorruption(float corruption)
        {
            _corruption = Mathf.Clamp01(corruption);
        }

        public void SetFacing(float x)
        {
            if (Mathf.Abs(x) > 0.01f) _facing = Mathf.Sign(x);
        }

        void Update()
        {
            // Ease the displayed value so crossing a band is a visible transformation
            // rather than a single-frame snap.
            _displayedCorruption = Mathf.MoveTowards(_displayedCorruption, _corruption, Time.deltaTime * 0.9f);
            ApplyCorruptionVisuals(_displayedCorruption, instant: false);
        }

        void ApplyCorruptionVisuals(float c, bool instant)
        {
            if (_body == null) return;

            var tint = Palette.Hero(c);
            _body.color = tint;
            // Slightly darker head, so the body reads as a figure rather than a blank pill.
            _head.color = new Color(tint.r * 0.72f, tint.g * 0.72f, tint.b * 0.78f, 1f);

            // Swell with corruption: the hero gets physically bigger and hunches wider.
            float swell = 1f + 0.34f * c;
            float hunch = 1f - 0.12f * c;
            _rig.localScale = new Vector3(_facing * swell, swell * hunch, 1f);

            _body.transform.localScale = new Vector3(
                _vessel.SilhouetteWidth * (1f + 0.22f * c),
                _vessel.SilhouetteHeight * (1f - 0.06f * c), 1f);

            // The head shrinks into the mass as the body takes over -- cheap, but it reads.
            _head.transform.localScale = Vector3.one * (0.42f - 0.10f * c);
            _head.transform.localPosition = new Vector3(0f, 0.40f - 0.05f * c, 0f);

            float auraAlpha = Mathf.Max(0f, c - PurityMeter.TaintedThreshold) * 0.55f;
            var band = PurityMeter.BandFor(c);
            var auraColor = Palette.For(band);
            auraColor.a = auraAlpha;
            _aura.color = auraColor;

            if (c >= PurityMeter.SelfDamageThreshold)
            {
                // Past the burn threshold the aura pulses, as a standing warning.
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 7f);
                _aura.transform.localScale = Vector3.one * 2.4f * pulse;
            }
            else
            {
                _aura.transform.localScale = Vector3.one * 2.4f;
            }
        }

        /// <summary>Brief white flash when the hero takes a hit.</summary>
        public void FlashHit()
        {
            StopCoroutine(nameof(HitFlash));
            StartCoroutine(nameof(HitFlash));
        }

        System.Collections.IEnumerator HitFlash()
        {
            if (_body == null) yield break;
            var original = _body.color;
            _body.color = Color.white;
            _head.color = Color.white;
            yield return new WaitForSeconds(0.07f);
            if (_body != null) _body.color = original;
            if (_head != null) _head.color = original;
        }
    }
}
