using Graft.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Graft.Game
{
    /// <summary>
    /// A floating virtual stick: the stick has no home on the screen, it appears wherever the thumb
    /// lands and reads the drag from there.
    ///
    /// **This is deliberately not a HUD decision.** STATUS.md records that nothing about UI layout,
    /// margins or safe areas is settled, and a fixed-position stick would quietly settle a corner
    /// of it. A floating stick needs no reserved region, so Phase 1 can ship movement without
    /// pre-empting Phase 4's HUD. Nothing is drawn on screen here either - the stick's *visuals*
    /// are a HUD question and are not being answered yet.
    ///
    /// Reads <see cref="Pointer"/> rather than <see cref="Touchscreen"/> so the same code path
    /// serves a thumb on the device and a mouse in the editor. One pointer only; multi-touch
    /// becomes relevant when there is a second control to touch.
    /// </summary>
    public class VirtualStickInput : MonoBehaviour, IMoveInput
    {
        [SerializeField] MovementTuning tuning;

        /// <summary>
        /// Used when <see cref="Screen.dpi"/> reports 0, which it does on some desktop setups.
        /// Roughly an iPhone's logical density, so an editor session feels like the device.
        /// </summary>
        const float FallbackDotsPerInch = 326f;

        const float CentimetresPerInch = 2.54f;

        Vector2 _origin;
        bool _engaged;

        public MovementTuning Tuning
        {
            get => tuning;
            set => tuning = value;
        }

        /// <summary>Where the stick currently considers its centre. Empty when nothing is pressed.</summary>
        public Vector2? Origin => _engaged ? _origin : (Vector2?)null;

        /// <summary>Where the thumb is, clamped to the travel radius. Read by the HUD to draw the knob.</summary>
        public Vector2 Knob { get; private set; }

        /// <summary>
        /// The travel radius in screen pixels. Public so the HUD draws a ring exactly the size of
        /// the region that actually does something - a ring that disagrees with the input is worse
        /// than no ring, because it teaches the player the wrong gesture.
        /// </summary>
        public float TravelRadiusPixels
        {
            get
            {
                var dpi = Screen.dpi > 1f ? Screen.dpi : FallbackDotsPerInch;
                var centimetres = tuning != null ? tuning.stickTravelCentimetres : 1.1f;
                return Mathf.Max(1f, centimetres * dpi / CentimetresPerInch);
            }
        }

        public Vec2 Read()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.isPressed)
            {
                _engaged = false;
                return Vec2.Zero;
            }

            var position = pointer.position.ReadValue();
            if (!_engaged)
            {
                _origin = position;
                _engaged = true;
                Knob = position;
                return Vec2.Zero;
            }

            var radius = TravelRadiusPixels;
            var delta = position - _origin;
            var distance = delta.magnitude;
            if (distance <= 1e-4f)
            {
                Knob = position;
                return Vec2.Zero;
            }

            if (distance > radius)
            {
                // Drag the origin along instead of clamping against it. Without this the thumb runs
                // out of travel on a long walk and the only way to keep going is to lift and re-press.
                _origin = position - delta * (radius / distance);
                distance = radius;
            }

            var direction = delta / delta.magnitude;
            var magnitude = distance / radius;
            Knob = _origin + direction * distance;
            return new Vec2(direction.x * magnitude, direction.y * magnitude);
        }
    }
}
