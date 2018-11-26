using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  public class GCHandleTests
  {
    [Fact]
    public void EnsureEnumerationStability()
    {
      // I made some changes to v4.5 handle enumeration to enumerate handles out faster.
      // This test makes sure I have a stable enumeration.
      using (var dt = TestTargets.GCHandles.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        List<ClrHandle> handles = new List<ClrHandle>(runtime.EnumerateHandles());

        int i = 0;
        foreach (var hnd in runtime.EnumerateHandles())
          Assert.Equal(handles[i++], hnd);

        // We create at least this many handles in the test, plus the runtime uses some.
        Assert.True(handles.Count > 4);
      }
    }

    [TestMethod]
    public void EnsureAllItemsAreUnique()
    {
      // Making sure that handles are returned only once
      var handles = new HashSet<ClrHandle>();

      using (var dt = TestTargets.GCHandles.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        foreach (var handle in runtime.EnumerateHandles())
          Assert.True(handles.Add(handle));

        // Make sure we had at least one AsyncPinned handle
        Assert.Contains(handles, h => h.HandleType == HandleType.AsyncPinned);
      }
    }
  }
}