using Microsoft.Diagnostics.Runtime.Utilities.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class DumpDebugTests
  {
    [TestMethod]
    public void SimpleCrashDumpDebugTest()
    {
      using (var dt = TestTargets.DumpDebug.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var corDebugProcess = runtime.TryGetCorDebugProcess();
        Assert.IsNotNull(corDebugProcess, "Failed to create ICorDebugProcess");

        corDebugProcess.EnumerateAppDomains(out var appDomains);
        Assert.IsNotNull(appDomains, "Failed to enumerate AppDomains");
        
        corDebugProcess.EnumerateThreads(out var threads);
        Assert.IsNotNull(threads, "Failed to enumerate Threads");
      }
    }
  }
}