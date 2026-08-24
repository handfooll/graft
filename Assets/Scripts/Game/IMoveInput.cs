using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// Where the movement intent comes from. An interface so the screenshot tour can drive the
    /// player without a thumb, and so the touch stick can be replaced without touching the
    /// controller - Phase 1's gate needs "mid-move" and "near a wall" captures, which nothing
    /// pointer-driven can produce under <c>-batchmode</c>.
    /// </summary>
    public interface IMoveInput
    {
        /// <summary>
        /// Raw stick reading, magnitude clamped to 1. Deadzone and rescaling are applied by the
        /// controller via <see cref="Graft.Core.Locomotion.ShapeStick"/>, so every input source
        /// gets the same shaping rather than each one inventing its own.
        /// </summary>
        Vec2 Read();
    }
}
