using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// How the top-down camera sits relative to the player (D2: fixed pitch, player centred).
    ///
    /// A ScriptableObject rather than constants, because these are tuning values and BUILD_PLAN.md
    /// keeps tuning in assets. Both the editor scene builder and the Phase 1 Cinemachine rig read
    /// this one asset, so there is never a second copy of the numbers to drift out of sync.
    ///
    /// Chosen against the sweeps in CameraFramingSweep, which put the real character, rings of
    /// enemy-sized capsules and stand-in graft props in every frame - so the three questions the
    /// framing has to answer at once (is the character readable, is a mutation readable, how much
    /// warning is there) get judged together instead of one at a time.
    ///
    /// Still provisional: "can I see a threat in time to react" is not truly answerable until
    /// Phase 3 puts enemies that actually converge on the screen.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraFraming", menuName = "GRAFT/Camera Framing")]
    public class CameraFraming : ScriptableObject
    {
        /// <summary>
        /// The device's screen aspect (width / height), 1179x2556 - an iPhone 14 Pro, which is the
        /// phone this is tested on and the one the XP Hero reference screenshot was taken on.
        ///
        /// Everything that reasons about how much world is on screen must use this and not 9:16.
        /// Portrait aspect is the single biggest lever on visible width, and 9:16 overstates it by
        /// more than a fifth.
        /// </summary>
        public const float DeviceAspect = 1179f / 2556f;

        [Header("Angle")]
        [Tooltip("Degrees below horizontal. 90 would be straight down. Shallower shows more of the " +
                 "character's silhouette, which is what makes facing direction readable.")]
        [Range(30f, 85f)]
        public float pitchDegrees = 47.5f;

        [Header("Distance")]
        [Tooltip("Metres from the player to the camera along the view direction. Together with the " +
                 "field of view this decides how much world is on screen - far more than pitch does.")]
        [Range(8f, 60f)]
        public float distance = 33.7f;

        [Header("Follow")]
        [Tooltip("Cinemachine position damping, in seconds. Kept small on purpose: the player is " +
                 "already accelerated smoothly, so damping has no jitter left to remove, and every " +
                 "extra tenth pushes the player off centre in the direction of travel - which costs " +
                 "exactly the forward view D2 centres them to get.")]
        [Range(0f, 1f)]
        public float positionDamping = 0.12f;

        [Header("Lens")]
        [Tooltip("Vertical field of view. Narrow on purpose. Unity measures FOV vertically, and " +
                 "portrait 9:16 makes a wide one very tall: at 45 degrees this camera spent 26 m of " +
                 "distance on only 12 m of screen width, left the character at 7% of screen height, " +
                 "and leant everything near the top and bottom of frame outward. Trading field of " +
                 "view for the same distance flattens that lean and gives 9 m of width instead.")]
        [Range(20f, 80f)]
        public float fieldOfView = 34f;

        public float Height => distance * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);

        /// <summary>How far behind the player the camera sits, on the ground plane.</summary>
        public float GroundSetback => distance * Mathf.Cos(pitchDegrees * Mathf.Deg2Rad);

        public Vector3 OffsetFromTarget => new Vector3(0f, Height, -GroundSetback);

        public Quaternion Rotation => Quaternion.Euler(pitchDegrees, 0f, 0f);

        /// <summary>
        /// Metres of world visible across the screen width, at the player's distance.
        /// Portrait is the reason this is worth computing: the horizontal field is far narrower
        /// than the vertical field of view makes it sound.
        /// </summary>
        public float VisibleWidth(float aspect)
        {
            return 2f * distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;
        }

        /// <summary>What the phone actually shows. This is the number to judge framing by.</summary>
        public float VisibleWidthOnDevice => VisibleWidth(DeviceAspect);

        /// <summary>
        /// Distance from the camera to where the top edge of the frame meets the ground - the
        /// furthest thing that can appear on screen.
        ///
        /// Anything sized to the camera has to be sized to THIS, not to <see cref="distance"/>.
        /// Shadow distance was originally a flat 25 m, which silently deleted every shadow in the
        /// frame as soon as the camera pulled back.
        /// </summary>
        public float FarGroundDistance
        {
            get
            {
                var topEdgeAngle = pitchDegrees - fieldOfView * 0.5f;
                // A shallow enough camera looks at the horizon and never meets the ground at all.
                return topEdgeAngle <= 1f
                    ? float.PositiveInfinity
                    : Height / Mathf.Sin(topEdgeAngle * Mathf.Deg2Rad);
            }
        }
    }
}
