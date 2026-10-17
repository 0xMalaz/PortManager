using Microsoft.VisualStudio.TestTools.UnitTesting;
using PortManager.Infrastructure;

[assembly: DoNotParallelize]

namespace PortManager.Tests;

[TestClass]
public static class TestAssembly
{
    // Tests assert English text regardless of the machine's display language.
    [AssemblyInitialize]
    public static void Initialize(TestContext context) => Strings.IsTurkish = false;
}
