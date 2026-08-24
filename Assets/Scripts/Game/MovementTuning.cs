using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The feel of moving, as an asset rather than as constants (BUILD_PLAN.md sequencing rule 3).
    /// Phase 1 exists to tune these four numbers, and they will be tuned again once there are
    /// enemies to run away from.
    /// </summary>
    [CreateAssetMenu(fileName = "MovementTuning", menuName = "GRAFT/Movement Tuning")]
    public class MovementTuning : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("Metres per second at full stick. Judge it against CameraFraming.VisibleWidth: " +
                 "this is how long the player has to react to something entering the frame.")]
        [Range(1f, 12f)]
        public float maxSpeed = 5.5f;

        [Tooltip("Seconds from standstill to full speed. Under ~0.1 the character feels weightless; " +
                 "over ~0.3 it feels like it is being dragged.")]
        [Range(0.02f, 0.6f)]
        public float timeToMaxSpeed = 0.16f;

        [Tooltip("Seconds from full speed to a stop once the thumb lifts. Deliberately shorter than " +
                 "the acceleration time - coasting past where you let go feels like a bug.")]
        [Range(0.02f, 0.6f)]
        public float timeToStop = 0.10f;

        [Header("Turning")]
        [Tooltip("Degrees per second. High enough to feel immediate, finite so that a reversal is a " +
                 "visible turn rather than a teleport - which is what makes facing readable at all.")]
        [Range(180f, 2160f)]
        public float turnDegreesPerSecond = 1440f;

        [Header("Stick")]
        [Tooltip("Fraction of the stick's travel that counts as no input at all. Input past this is " +
                 "rescaled from zero, so crossing the deadzone does not jump straight to a walk.")]
        [Range(0f, 0.5f)]
        public float stickDeadzone = 0.12f;

        [Tooltip("Physical thumb travel, in centimetres, that reads as full stick. In centimetres " +
                 "and not pixels on purpose: a thumb is the same size on every iPhone, and a pixel " +
                 "radius silently means a different gesture on each one.")]
        [Range(0.4f, 3f)]
        public float stickTravelCentimetres = 1.1f;

        public MovementProfile Profile =>
            new MovementProfile(maxSpeed, timeToMaxSpeed, timeToStop, turnDegreesPerSecond);
    }
}
