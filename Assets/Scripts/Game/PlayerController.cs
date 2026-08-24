using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Drives the player capsule from a stick reading. All the feel lives in
    /// <see cref="Graft.Core.Locomotion"/>; this class only converts between the ground plane and
    /// Unity's world, and hands the result to a CharacterController.
    ///
    /// A CharacterController rather than a Rigidbody: nothing in this game wants the player to be
    /// pushed, and sliding along a wall instead of sticking to it is behaviour worth getting for
    /// free - Phase 1's third gate image is the player against a wall.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] MovementTuning tuning;

        /// <summary>
        /// Enough downward push to keep the controller reporting grounded on flat ground without
        /// accumulating a fall. Phase 1 has no jumps, drops or slopes, so this is all gravity needs
        /// to be; a real fall model can arrive when there is something to fall off.
        /// </summary>
        const float GroundStick = -4f;

        CharacterController _controller;
        IMoveInput _input;
        LocomotionState _state = LocomotionState.AtRest();

        public MovementTuning Tuning
        {
            get => tuning;
            set => tuning = value;
        }

        /// <summary>
        /// Swappable so the screenshot tour can pose the player. Set before or after Awake; a value
        /// set here always wins over the component found on the object.
        /// </summary>
        public IMoveInput Input
        {
            get => _input;
            set => _input = value;
        }

        /// <summary>Metres per second on the ground plane. Read by the tour to prove "mid-move".</summary>
        public float Speed => _state.Speed;

        public float Heading => _state.Heading;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input ??= GetComponent<IMoveInput>();
            _state = LocomotionState.AtRest(transform.eulerAngles.y);
        }

        void Update()
        {
            if (tuning == null)
            {
                return;
            }

            var raw = _input != null ? _input.Read() : Vec2.Zero;
            var intent = Locomotion.ShapeStick(raw, tuning.stickDeadzone);
            _state = Locomotion.Step(_state, intent, tuning.Profile, Time.deltaTime);

            // The stick is read in screen axes and applied in world axes with no transform between
            // them. That is only correct because D2 fixes the camera's yaw at zero and forbids
            // player-controlled rotation - if that ever changes, this is the line that breaks.
            var motion = new Vector3(_state.Velocity.X, GroundStick, _state.Velocity.Y);
            _controller.Move(motion * Time.deltaTime);

            transform.rotation = Quaternion.Euler(0f, _state.Heading, 0f);
        }

        /// <summary>
        /// Drops the player at a spot with no velocity, without the CharacterController fighting
        /// the assignment. Used to pose gate screenshots and, later, to respawn at Base (§3).
        /// </summary>
        public void Teleport(Vector3 position, float heading)
        {
            var controller = _controller != null ? _controller : GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            controller.enabled = true;
            _state = LocomotionState.AtRest(heading);
        }
    }
}
