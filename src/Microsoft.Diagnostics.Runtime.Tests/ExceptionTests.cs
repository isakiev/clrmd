using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class ExceptionTests
  {
    [TestMethod]
    public void ExceptionPropertyTest()
    {
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        TestProperties(runtime);
      }
    }

    public static void TestProperties(ClrRuntime runtime)
    {
      var thread = runtime.Threads.Where(t => !t.IsFinalizer).Single();
      var ex = thread.CurrentException;
      Assert.IsNotNull(ex);

      var testData = TestTargets.NestedExceptionData;
      Assert.AreEqual(testData.OuterExceptionMessage, ex.Message);
      Assert.AreEqual(testData.OuterExceptionType, ex.Type.Name);
      Assert.IsNotNull(ex.Inner);
    }
  }
}