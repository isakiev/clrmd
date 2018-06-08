using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class FinalizationQueueTests
  {
    [TestMethod]
    public void TestQueuedObjectsCount()
    {
      using (var dt = TestTargets.FinalizationQueue.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var targetObjectsCount = 0;

        foreach (var address in runtime.EnumerateFinalizerQueueObjectAddresses())
        {
          var type = runtime.Heap.GetObjectType(address);
          if (type.Name == typeof(DieFast).FullName)
            targetObjectsCount++;
        }
        
        Assert.AreEqual(FinalizationQueueTarget.ObjectsCount, targetObjectsCount);
      }
    }
  }
}