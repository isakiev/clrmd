using System.Collections.Generic;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  public class FinalizableObjectsTests
  {
    [Fact]
    public void TestAllFinalizableObjects()
    {
      using (var dt = TestTargets.FinalizableObjects.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var stats = GetStats(runtime.Heap, runtime.Heap.EnumerateFinalizableObjectAddresses());

        Assert.Equal(0, stats.A);
        Assert.Equal(FinalizableObjectsTarget.ObjectsCountB, stats.B);
        Assert.Equal(FinalizableObjectsTarget.ObjectsCountC, stats.C);
      }
    }

    [Fact]
    public void TestFinalizerQueueObjects()
    {
      using (var dt = TestTargets.FinalizableObjects.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var stats = GetStats(runtime.Heap, runtime.EnumerateFinalizerQueueObjectAddresses());

        Assert.Equal(FinalizableObjectsTarget.ObjectsCountA, stats.A);
        Assert.Equal(0, stats.B);
        Assert.Equal(0, stats.C);
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