using System.Linq;
using Graft.Core;
using NUnit.Framework;

namespace Graft.Tests
{
    /// <summary>
    /// DESIGN.md non-negotiable: <c>Graft.Core</c> never references UnityEngine, so the rules stay
    /// headlessly testable and survive an engine change. The asmdef flag that enforces this
    /// (<c>noEngineReferences</c>) is one checkbox away from being lost, so assert the outcome.
    /// </summary>
    public class AssemblyBoundaryTests
    {
        [Test]
        public void CoreDoesNotReferenceUnity()
        {
            var offenders = typeof(DesignContract).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor"))
                .ToArray();

            Assert.IsEmpty(offenders,
                "Graft.Core picked up an engine reference: " + string.Join(", ", offenders));
        }
    }
}
