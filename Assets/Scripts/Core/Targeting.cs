using System.Collections.Generic;

namespace Graft.Core
{
    /// <summary>
    /// Who to shoot. BUILD_PLAN.md Phase 3: "player auto-attacks nearest target in range" - which
    /// is one sentence and two decisions, both of which are felt rather than seen, so both live
    /// here where a test can hold them still.
    /// </summary>
    public static class Targeting
    {
        /// <summary>
        /// Index of the nearest candidate within <paramref name="range"/>, or -1 if none is.
        ///
        /// Ties go to the lower index, which in practice means the enemy that has been alive
        /// longest. Any tie-break would do; having one at all is what stops the auto-attack
        /// flickering between two equidistant targets and landing nothing.
        /// </summary>
        public static int NearestWithin(Vec2 origin, IReadOnlyList<Vec2> candidates, float range)
        {
            if (candidates == null || range <= 0f)
            {
                return -1;
            }

            var limit = range * range;
            var best = -1;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var offset = candidates[i] - origin;
                var distance = offset.SqrMagnitude;
                if (distance > limit || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        /// <summary>
        /// Whether a target that was already acquired stays acquired. Deliberately more generous
        /// than acquisition: without the margin a target sitting exactly on the range boundary is
        /// picked up and dropped on alternate frames, and the auto-attack never completes a shot.
        /// </summary>
        public const float HysteresisMargin = 1.15f;

        public static bool StaysAcquired(Vec2 origin, Vec2 target, float range) =>
            (target - origin).SqrMagnitude <= range * HysteresisMargin * (range * HysteresisMargin);
    }
}
