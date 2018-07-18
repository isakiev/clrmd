using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class FinalizableObjectsTests
  {
    [TestMethod]
    public void TestAllFinalizableObjects()
    {
      using (var dt = TestTargets.FinalizableObjects.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var stats = GetStats(runtime.Heap, runtime.Heap.EnumerateFinalizableObjectAddresses());

        Assert.AreEqual(0, stats.A);
        Assert.AreEqual(FinalizableObjectsTarget.ObjectsCountB, stats.B);
        Assert.AreEqual(FinalizableObjectsTarget.ObjectsCountC, stats.C);
      }
    }

    [TestMethod]
    public void TestFinalizerQueueObjects()
    {
      using (var dt = TestTargets.FinalizableObjects.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var stats = GetStats(runtime.Heap, runtime.EnumerateFinalizerQueueObjectAddresses());

        Assert.AreEqual(FinalizableObjectsTarget.ObjectsCountA, stats.A);
        Assert.AreEqual(0, stats.B);
        Assert.AreEqual(0, stats.C);
      }
    }

    private static Stats GetStats(ClrHeap heap, IEnumerable<ulong> addresses)
    {
      var stats = new Stats();
      foreach (var address in addresses)
      {
        var type = heap.GetObjectType(address);
        if (type.Name == typeof(SampleA).FullName)
          stats.A++;
        else if (type.Name == typeof(SampleB).FullName)
          stats.B++;
        else if (type.Name == typeof(SampleC).FullName)
          stats.C++;
      }

      return stats;
    }

    private class Stats
    {
      public int A;
      public int B;
      public int C;
    }
  }
}