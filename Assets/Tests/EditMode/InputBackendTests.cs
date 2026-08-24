using NUnit.Framework;

namespace Graft.Tests
{
    /// <summary>
    /// D8 puts touch controls in from the start, and Phase 1's virtual stick is built on the Input
    /// System package. Having the legacy Input Manager active as well means two input paths that
    /// silently disagree, so the project is set to Input System only. The compile-time defines are
    /// the honest way to check it - they are what the rest of the code actually branches on.
    /// </summary>
    public class InputBackendTests
    {
        [Test]
        public void InputSystemIsTheOnlyBackend()
        {
#if !ENABLE_INPUT_SYSTEM
            Assert.Fail("Input System backend is off. Set activeInputHandler to 1 in ProjectSettings.asset.");
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            Assert.Fail("Legacy Input Manager is still active - two input paths. Expected activeInputHandler 1, not 2.");
#endif
            Assert.Pass();
        }
    }
}
