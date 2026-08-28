using System;

namespace Graft.Core
{
    /// <summary>
    /// Paces repeated attacks. Both the player's auto-attack and every enemy's run on one of these.
    ///
    /// The rule worth writing down: **the first attack after acquiring a target is immediate.**
    /// A cooldown that starts full means walking into range and then standing there for the length
    /// of an interval doing nothing, which reads as the game not having noticed. The clock winds
    /// down while idle and never banks more than one shot, so an enemy that has been chasing for
    /// ten seconds still only gets one hit on arrival.
    /// </summary>
    public struct AttackClock
    {
        float _remaining;

        /// <summary>Seconds until the next attack is allowed. Zero means ready.</summary>
        public float Remaining => _remaining;

        public bool IsReady => _remaining <= 0f;

        /// <summary>
        /// Advances the clock and reports whether an attack fires this step. Fires at most once per
        /// call: a frame long enough to cover two intervals still only produces one attack, because
        /// the alternative is a hitch turning into a burst of free damage.
        /// </summary>
        public bool Tick(float deltaTime, bool hasTarget, float interval)
        {
            if (deltaTime > 0f)
            {
                _remaining = MathF.Max(0f, _remaining - deltaTime);
            }

            if (!hasTarget || _remaining > 0f)
            {
                return false;
            }

            _remaining = MathF.Max(0.05f, interval);
            return true;
        }

        /// <summary>Puts a full interval back on the clock - used when something interrupts.</summary>
        public void Delay(float seconds) => _remaining = MathF.Max(_remaining, MathF.Max(0f, seconds));
    }
}
