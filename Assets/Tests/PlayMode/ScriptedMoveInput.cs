using Graft.Core;
using Graft.Game;

namespace Graft.Tests
{
    /// <summary>
    /// A stick with no thumb on it. Under <c>-batchmode</c> there is no pointer device at all, so
    /// the gate's "mid-move" and "near a wall" captures are impossible to produce through
    /// <see cref="VirtualStickInput"/> - which is the reason <see cref="IMoveInput"/> exists.
    /// </summary>
    public class ScriptedMoveInput : IMoveInput
    {
        public Vec2 Value = Vec2.Zero;

        public Vec2 Read() => Value;
    }
}
