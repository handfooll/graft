using System.Collections.Generic;

namespace Graft.Core
{
    /// <summary>
    /// Essence is not a single XP bar (GDD S4) -- there is one meter per enemy family, and
    /// filling one is what offers a graft built from *that* family. Killing a varied mix
    /// therefore progresses several meters slowly; farming one type spikes a single option.
    /// </summary>
    public sealed class EssenceWallet
    {
        readonly Dictionary<string, int> _amounts = new Dictionary<string, int>();

        public int AmountFor(EnemyArchetype a) => _amounts.TryGetValue(a.Id, out var v) ? v : 0;

        public float FillFractionFor(EnemyArchetype a)
        {
            float f = (float)AmountFor(a) / a.EssenceToGraft;
            return f > 1f ? 1f : f;
        }

        public bool IsReady(EnemyArchetype a) => AmountFor(a) >= a.EssenceToGraft;

        public void Add(EnemyArchetype a, int amount = 1)
        {
            _amounts[a.Id] = AmountFor(a) + amount;
        }

        /// <summary>Spends one meter's worth. Overflow carries into the next fill, so a big
        /// kill streak is never wasted.</summary>
        public bool ConsumeGraftCharge(EnemyArchetype a)
        {
            if (!IsReady(a)) return false;
            _amounts[a.Id] = AmountFor(a) - a.EssenceToGraft;
            return true;
        }

        /// <summary>Every family currently sitting at a full meter, in catalog order.</summary>
        public List<EnemyArchetype> ReadyArchetypes()
        {
            var ready = new List<EnemyArchetype>();
            for (int i = 0; i < ArchetypeCatalog.All.Count; i++)
            {
                var a = ArchetypeCatalog.All[i];
                if (IsReady(a)) ready.Add(a);
            }
            return ready;
        }

        public void Clear() => _amounts.Clear();
    }
}
