using Microsoft.Diagnostics.Runtime.Utilities.Runtime;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  public class DumpDebugTests
  {
    [Fact]
    public void SimpleCrashDumpDebugTest()
    {
      using (var dt = TestTargets.DumpDebug.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var corDebugProcess = runtime.TryGetCorDebugProcess();
        Assert.NotNull(corDebugProcess);

        corDebugProcess.EnumerateAppDomains(out var appDomains);
        Assert.NotNull(appDomains);

        corDebugProcess.EnumerateThreads(out var threads);
        Assert.NotNull(threads);
      }
    }
  }
}