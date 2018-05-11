using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class GCHandleTests
  {
    [TestMethod]
    public void EnsureEnumerationStability()
    {
      // I made some changes to v4.5 handle enumeration to enumerate handles out faster.
      // This test makes sure I have a stable enumeration.
      using (var dt = TestTargets.GCHandles.LoadFullDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();

        var handles = new List<ClrHandle>();

        bool cont;
        do
        {
          cont = false;
          var i = 0;
          foreach (var hnd in runtime.EnumerateHandles())
          {
            if (i > handles.Count)
              break;

            if (i == handles.Count)
            {
              cont = true;
              handles.Add(hnd);
              break;
            }

            Assert.AreEqual(handles[i++], hnd);
          }
        } while (cont);

        // We create at least this many handles in the test, plus the runtime uses some.
        Assert.IsTrue(handles.Count > 4);
      }
    }

    [TestMethod]
    public void EnsureAllItemsAreUnique()
    {
      // Making sure that handles are returned only once
      var handles = new HashSet<ClrHandle>();

      using (var dt = TestTargets.GCHandles.LoadFullDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();

        foreach (var handle in runtime.EnumerateHandles()) Assert.IsTrue(handles.Add(handle));

        // Make sure we had at least one AsyncPinned handle
        Assert.IsTrue(handles.Any(h => h.HandleType == HandleType.AsyncPinned));
      }
    }
  }
}