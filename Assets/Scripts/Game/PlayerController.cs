using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// Movement and the one deliberate input the game has: the dash. Attacks are automatic
    /// (see <see cref="AbilityRunner"/>), so a thumb on the stick is the whole control scheme.
    /// Reads a virtual stick on touch devices and WASD/arrows plus space in the editor.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public Vector2 Facing { get; private set; } = Vector2.up;
        public bool IsDashing => _dashRemaining > 0f;

        const float DashDuration = 0.18f;
        const float DashSpeedMultiplier = 4.2f;
        const float DashCooldown = 1.1f;

        RunContext _ctx;
        PlayerAvatar _avatar;
        AbilityRunner _abilities;
        VirtualStick _stick;

        float _dashRemaining;
        float _dashCooldownRemaining;
        Vector2 _dashDirection;

        public float DashCooldownFraction =>
            DashCooldown <= 0f ? 0f : Mathf.Clamp01(_dashCooldownRemaining / DashCooldown);

        public void Init(RunContext ctx, PlayerAvatar avatar, AbilityRunner abilities, VirtualStick stick)
        {
            _ctx = ctx;
            _avatar = avatar;
            _abilities = abilities;
            _stick = stick;
        }

        void Update()
        {
            if (_ctx == null || _ctx.Run.IsDead) return;

            float dt = Time.deltaTime;
            _dashCooldownRemaining = Mathf.Max(0f, _dashCooldownRemaining - dt);

            Vector2 input = ReadMoveInput();
            if (input.sqrMagnitude > 1f) input.Normalize();

            if (input.sqrMagnitude > 0.01f)
            {
                Facing = input.normalized;
                _avatar.SetFacing(input.x);
            }

            float speed = _ctx.Run.EffectiveMoveSpeed;
            Vector2 velocity;

            if (_dashRemaining > 0f)
            {
                _dashRemaining -= dt;
                velocity = _dashDirection * speed * DashSpeedMultiplier;
            }
            else
            {
                velocity = input * speed;
                if (ReadDashPressed() && _dashCooldownRemaining <= 0f)
                {
                    StartDash(input.sqrMagnitude > 0.01f ? input.normalized : Facing);
                }
            }

            Vector2 next = (Vector2)transform.position + velocity * dt;
            transform.position = Arena.Instance.Clamp(next);
        }

        void StartDash(Vector2 direction)
        {
            _dashDirection = direction;
            _dashRemaining = DashDuration;
            _dashCooldownRemaining = DashCooldown;
            _abilities.OnDash(direction);
        }

        /// <summary>Public so a HUD button can drive the dash on touch.</summary>
        public void RequestDash()
        {
            if (_dashCooldownRemaining > 0f || _dashRemaining > 0f) return;
            StartDash(Facing);
        }

        Vector2 ReadMoveInput()
        {
            if (_stick != null && _stick.IsActive) return _stick.Value;

            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            return new Vector2(x, y);
        }

        static bool ReadDashPressed() => Input.GetKeyDown(KeyCode.Space);
    }
}
