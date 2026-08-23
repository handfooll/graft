using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// The narrow surface combat objects need from the run. Keeping it an interface means
    /// enemies and abilities never reach into the game controller, and the whole combat
    /// layer stays swappable.
    /// </summary>
    public interface RunContext
    {
        RunState Run { get; }
        void DamagePlayer(float amount);
        void OnEnemyKilled(EnemyAgent enemy);
    }
}
