using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class ModuleTests
  {
    [TestMethod]
    public void TestGetTypeByName()
    {
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        var heap = runtime.Heap;

        var shared = runtime.GetModule("sharedlibrary.dll");
        Assert.IsNotNull(shared.GetTypeByName("Foo"));
        Assert.IsNull(shared.GetTypeByName("Types"));

        var types = runtime.GetModule("types.exe");
        Assert.IsNotNull(types.GetTypeByName("Types"));
        Assert.IsNull(types.GetTypeByName("Foo"));
      }
    }
  }
}