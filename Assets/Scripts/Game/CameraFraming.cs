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
    /// Chosen against a real XP Hero screenshot plus the pitch/zoom sweeps in CameraPitchSweep.
    /// Treat them as provisional: the question "can I see a threat in time to react" cannot really
    /// be answered until Phase 3 puts actual enemies on the screen.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraFraming", menuName = "GRAFT/Camera Framing")]
    public class CameraFraming : ScriptableObject
    {
        [Header("Angle")]
        [Tooltip("Degrees below horizontal. 90 would be straight down. Shallower shows more of the " +
                 "character's silhouette, which is what makes facing direction readable.")]
        [Range(30f, 85f)]
        public float pitchDegrees = 55f;

        [Header("Distance")]
        [Tooltip("Metres from the player to the camera along the view direction. This is the knob " +
                 "that decides how much world is on screen - far more than pitch does.")]
        [Range(8f, 60f)]
        public float distance = 26f;

        [Header("Lens")]
        [Range(20f, 80f)]
        public float fieldOfView = 45f;

        public float Height => distance * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);

        /// <summary>How far behind the player the camera sits, on the ground plane.</summary>
        public float GroundSetback => distance * Mathf.Cos(pitchDegrees * Mathf.Deg2Rad);

        public Vector3 OffsetFromTarget => new Vector3(0f, Height, -GroundSetback);

        public Quaternion Rotation => Quaternion.Euler(pitchDegrees, 0f, 0f);

        /// <summary>
        /// Metres of world visible across the screen width, at the player's distance.
        /// Portrait is the reason this is worth computing: at 9:16 the horizontal field is far
        /// narrower than the vertical field of view makes it sound.
        /// </summary>
        public float VisibleWidth(float aspect)
        {
            return 2f * distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;
        }

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
