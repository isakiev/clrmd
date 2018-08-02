using System.Linq;
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
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                var corDebugProcess = runtime.TryGetCorDebugProcess();
                Assert.True(corDebugProcess != null, "Failed to create ICorDebugProcess");

                corDebugProcess.EnumerateAppDomains(out var appDomains);
                Assert.True(appDomains != null, "Failed to enumerate AppDomains");

                corDebugProcess.EnumerateThreads(out var threads);
                Assert.True(threads != null, "Failed to enumerate Threads");
            }
        }
    }
}