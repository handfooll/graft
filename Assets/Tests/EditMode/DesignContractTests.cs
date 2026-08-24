using Graft.Core;
using NUnit.Framework;

namespace Graft.Tests
{
    /// <summary>
    /// Guards the locked decisions in DESIGN.md §2. These are not tuning values - if one of these
    /// fails, either the design document changed or something drifted away from it silently.
    /// </summary>
    public class DesignContractTests
    {
        [Test]
        public void FiveBiomesPlusBase()
        {
            Assert.AreEqual(5, DesignContract.BiomeCount);
            Assert.AreEqual(6, DesignContract.AreaCount);
        }

        [Test]
        public void FiveBodySlots()
        {
            Assert.AreEqual(DesignContract.BodySlotCount, System.Enum.GetValues(typeof(BodySlot)).Length);
        }

        [Test]
        public void SelfDamageStartsAt85Percent()
        {
            Assert.AreEqual(0.85f, DesignContract.SelfDamageThreshold, 0.0001f);
        }
    }
}
