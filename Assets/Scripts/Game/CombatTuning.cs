using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// How fighting feels, and how it reads. One asset, because the two are the same question:
    /// the reach of the auto-attack decides how close you let things get, and the size of a health
    /// bar decides whether you can tell.
    ///
    /// Judge the reach against <see cref="CameraFraming.VisibleWidthOnDevice"/>, not in metres.
    /// A range longer than half the visible width means shooting things you cannot see, which is
    /// how a top-down game ends up played by watching the health bar instead of the screen.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "GRAFT/Combat Tuning")]
    public class CombatTuning : ScriptableObject
    {
        [Header("The player")]
        [Range(20f, 500f)] public float playerMaxHealth = 100f;

        [Tooltip("Metres. About two thirds of the visible width, so a target is on screen well " +
                 "before it is shootable and you get the choice of engaging or walking on.")]
        [Range(2f, 14f)] public float attackRange = 6.5f;

        [Tooltip("Seconds between shots. With the damage below this is ~27 damage per second, " +
                 "which kills a Verge Chitter in under a second and a Bone Reach Shellback in six.")]
        [Range(0.1f, 2f)] public float attackInterval = 0.45f;

        [Range(1f, 100f)] public float attackDamage = 12f;

        [Tooltip("Seconds of standing in Base before a beaten-up player is whole again. Phase 4 " +
                 "replaces this with banking; until then something has to close the loop.")]
        [Range(0f, 20f)] public float baseHealSeconds = 4f;

        [Header("Projectiles")]
        [Tooltip("Metres per second. Fast enough that leading a target is never the skill being " +
                 "tested, slow enough that a shot in flight is a visible thing on screen.")]
        [Range(8f, 90f)] public float projectileSpeed = 30f;

        [Range(0.05f, 0.6f)] public float projectileRadius = 0.22f;

        [Tooltip("Metres per second for an enemy's shot. Slower than the player's on purpose - " +
                 "Spark's shots are meant to be seen coming and stepped out of.")]
        [Range(4f, 40f)] public float enemyProjectileSpeed = 14f;

        [Header("Hit feedback")]
        [Tooltip("Seconds an enemy flashes on being hit. Short: the flash has to end before the " +
                 "next shot lands or a swarm under fire is a solid white blob.")]
        [Range(0.02f, 0.4f)] public float hitFlashSeconds = 0.09f;

        [Tooltip("Seconds a dying enemy takes to collapse and vanish. Long enough to register as " +
                 "a kill, short enough not to leave corpses standing in the way of the next shot.")]
        [Range(0.05f, 1.5f)] public float deathSeconds = 0.35f;

        [Tooltip("Metres the impact burst grows to. Sized against the enemies, not the world.")]
        [Range(0.2f, 3f)] public float impactBurstRadius = 1.1f;

        [Header("Health bars")]
        [Tooltip("Metres wide in world space. The camera pitch and distance never change (D2), so " +
                 "a world-space bar is a fixed size on screen - which is the thing a screen-space " +
                 "bar would have needed a projection every frame to achieve.")]
        [Range(0.4f, 3f)] public float healthBarWidth = 1.15f;

        [Range(0.04f, 0.5f)] public float healthBarHeight = 0.15f;

        [Tooltip("Metres above the enemy's own height. Clear of the head, under the next enemy's feet.")]
        [Range(0f, 1.5f)] public float healthBarLift = 0.34f;

        public Color healthBarFill = new Color(0.94f, 0.30f, 0.26f);
        public Color healthBarBackdrop = new Color(0.06f, 0.06f, 0.07f, 1f);

        [Header("Materials")]
        [Tooltip("Filled in by ProjectSetup from MaterialLibrary - referenced from the asset so an " +
                 "iOS build cannot strip the shaders out from under runtime-created materials.")]
        public Material playerProjectileMaterial;
        public Material enemyProjectileMaterial;
        public Material hitFlashMaterial;
        public Material healthBarFillMaterial;
        public Material healthBarBackdropMaterial;

        public float DamagePerSecond => attackDamage / Mathf.Max(0.01f, attackInterval);

        /// <summary>
        /// Reach as a fraction of the screen's width. The number this asset is actually judged on:
        /// past about 0.75 the auto-attack is reaching off screen.
        /// </summary>
        public float RangeAsFractionOfScreen(CameraFraming framing) =>
            framing == null ? 0f : attackRange / Mathf.Max(0.01f, framing.VisibleWidthOnDevice);
    }
}
