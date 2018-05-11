using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class HeapTests
  {
    [TestMethod]
    public void HeapEnumeration()
    {
      // Simply test that we can enumerate the heap.
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        var heap = runtime.Heap;

        var encounteredFoo = false;
        var count = 0;
        foreach (var obj in heap.EnumerateObjectAddresses())
        {
          var type = heap.GetObjectType(obj);
          Assert.IsNotNull(type);
          if (type.Name == "Foo")
            encounteredFoo = true;

          count++;
        }

        Assert.IsTrue(encounteredFoo);
        Assert.IsTrue(count > 0);
      }
    }

    [TestMethod]
    public void HeapEnumerationMatches()
    {
      // Simply test that we can enumerate the heap.
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        var heap = runtime.Heap;

        var objects = new List<ClrObject>(heap.EnumerateObjects());

        var count = 0;
        foreach (var obj in heap.EnumerateObjectAddresses())
        {
          var actual = objects[count++];

          Assert.AreEqual(obj, actual.Address);

          var type = heap.GetObjectType(obj);
          Assert.AreEqual(type, actual.Type);
        }

        Assert.IsTrue(count > 0);
      }
    }

    [TestMethod]
    public void HeapCachedEnumerationMatches()
    {
      // Simply test that we can enumerate the heap.
      using (var dt = TestTargets.Types.LoadFullDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        var heap = runtime.Heap;

        var expectedList = new List<ClrObject>(heap.EnumerateObjects());

        heap.CacheHeap(CancellationToken.None);
        Assert.IsTrue(heap.IsHeapCached);
        var actualList = new List<ClrObject>(heap.EnumerateObjects());

        Assert.IsTrue(actualList.Count > 0);
        Assert.AreEqual(expectedList.Count, actualList.Count);

        for (var i = 0; i < actualList.Count; i++)
        {
          var expected = expectedList[i];
          var actual = actualList[i];

          Assert.IsTrue(expected == actual);
          Assert.AreEqual(expected, actual);
        }
      }
    }

    [TestMethod]
    public void ServerSegmentTests()
    {
      using (var dt = TestTargets.Types.LoadFullDump(GCMode.Server))
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        var heap = runtime.Heap;

        Assert.IsTrue(runtime.ServerGC);

        CheckSegments(heap);
      }
    }

    [TestMethod]
    public void WorkstationSegmentTests()
    {
      using (var dt = TestTargets.Types.LoadFullDump(GCMode.Workstation))
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        var heap = runtime.Heap;

        Assert.IsFalse(runtime.ServerGC);

        CheckSegments(heap);
      }
    }

    private static void CheckSegments(ClrHeap heap)
    {
      foreach (var seg in heap.Segments)
      {
        Assert.AreNotEqual(0ul, seg.Start);
        Assert.AreNotEqual(0ul, seg.End);
        Assert.IsTrue(seg.Start <= seg.End);

        Assert.IsTrue(seg.Start < seg.CommittedEnd);
        Assert.IsTrue(seg.CommittedEnd < seg.ReservedEnd);

        if (!seg.IsEphemeral)
        {
          Assert.AreEqual(0ul, seg.Gen0Length);
          Assert.AreEqual(0ul, seg.Gen1Length);
        }

        foreach (var obj in seg.EnumerateObjectAddresses())
        {
          var curr = heap.GetSegmentByAddress(obj);
          Assert.AreSame(seg, curr);
        }
      }
    }
  }
}