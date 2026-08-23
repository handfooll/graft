namespace Graft.Core
{
    /// <summary>
    /// A fully resolved ability: what it does, how it comes out, and what shape it takes.
    /// Produced by <see cref="GraftResolver"/> from (archetype x slot x weapon) and read
    /// directly by the combat layer, which never needs to know where the numbers came from.
    /// </summary>
    public struct AbilitySpec
    {
        public string DisplayName;
        public string Description;

        public Element Element;
        public Delivery Delivery;
        public WeaponClass Weapon;

        /// <summary>Damage per activation, before Power scaling and corruption multipliers.</summary>
        public float Damage;
        public float Cooldown;
        public float Range;
        /// <summary>Width of the affected arc in degrees. 360 for an aura.</summary>
        public float ArcDegrees;
        public int ProjectileCount;
        public float ProjectileSpeed;

        /// <summary>Venom: damage over time left on whatever was hit.</summary>
        public float DotDamagePerSecond;
        public float DotDuration;
        /// <summary>Spark: how many extra targets the hit jumps to.</summary>
        public int ChainCount;
        /// <summary>Impact: how hard the hit shoves the target.</summary>
        public float Knockback;

        public bool IsPassiveOnly => Damage <= 0f && DotDamagePerSecond <= 0f;
    }
}
