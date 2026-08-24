using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The character's silhouette, as an asset.
    ///
    /// This exists because the primitive stand-in is not just a placeholder - the camera is framed
    /// against it, Phase 5's sockets hang off it, and corruption swell is measured in it. A capsule
    /// whose shape does not resemble what Phase 6 actually imports makes every one of those
    /// decisions against the wrong silhouette.
    ///
    /// Head size is the knob that matters most. It is very nearly the only thing separating a
    /// realistic figure from a stylised one, and at a top-down camera it is what makes a character
    /// read as a character rather than as a stick.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterProportions", menuName = "GRAFT/Character Proportions")]
    public class CharacterProportions : ScriptableObject
    {
        [Tooltip("Metres from the floor to the top of the head. Also the CharacterController height.")]
        [Range(0.8f, 2.2f)]
        public float height = 1.5f;

        [Tooltip("Shoulder width in metres - the capsule's diameter, and twice the controller radius.")]
        [Range(0.4f, 1.4f)]
        public float width = 0.95f;

        [Tooltip("Head height as a fraction of total height. 1/8 is anatomically realistic; a quarter " +
                 "reads as stylised; two fifths is chibi. Divide 1 by this to get the classic " +
                 "'heads tall' figure.")]
        [Range(0.1f, 0.45f)]
        public float headFraction = 0.32f;

        /// <summary>The classic artist's measure: total height divided by head height.</summary>
        public float HeadsTall => 1f / Mathf.Max(0.01f, headFraction);

        public float HeadDiameter => height * headFraction;

        /// <summary>Height of the body capsule alone, from the floor to the base of the head.</summary>
        public float BodyHeight => Mathf.Max(0.05f, height - HeadDiameter);
    }
}
