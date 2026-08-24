using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Applies <see cref="CameraFraming"/> to a Cinemachine camera. The asset stays the single
    /// source of truth for the framing: STATUS.md records that the numbers are provisional until
    /// Phase 3 puts real enemies on screen, and the whole point of them being an asset is that
    /// retuning is a value change and not a refactor.
    ///
    /// Top-down with a fixed pitch (D2), so there is deliberately **no Cinemachine rotation
    /// component**. An aim component would re-derive the rotation from the damped position every
    /// frame, and the pitch would breathe as the camera catches up - the one thing D2 rules out.
    /// With no rotation stage, the camera keeps the rotation set here.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    [ExecuteAlways]
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] CameraFraming framing;
        [SerializeField] Transform target;

        public CameraFraming Framing
        {
            get => framing;
            set => framing = value;
        }

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        void OnEnable() => Apply();

        void OnValidate() => Apply();

        /// <summary>
        /// Idempotent - the editor scene builder calls it so the saved scene already looks right,
        /// and it runs again at load so an edited asset takes effect without rebuilding the scene.
        /// </summary>
        public void Apply()
        {
            if (framing == null)
            {
                return;
            }

            var camera = GetComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = framing.fieldOfView;
            camera.Lens.NearClipPlane = 0.3f;
            camera.Lens.FarClipPlane = framing.FarGroundDistance * 2f;
            camera.Target.TrackingTarget = target;
            camera.Target.LookAtTarget = null;

            var follow = GetComponent<CinemachineFollow>();
            if (follow != null)
            {
                // World space, not target-local: the player rotates constantly and a target-local
                // offset would swing the camera around them every time they turned.
                follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
                follow.TrackerSettings.PositionDamping = Vector3.one * framing.positionDamping;
                follow.FollowOffset = framing.OffsetFromTarget;
            }

            var anchor = target != null ? target.position : Vector3.zero;
            transform.SetPositionAndRotation(anchor + framing.OffsetFromTarget, framing.Rotation);
        }
    }
}
