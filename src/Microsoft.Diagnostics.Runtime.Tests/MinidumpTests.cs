using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  internal class StackTraceEntry
  {
    public ClrStackFrameType Kind { get; set; }
    public string ModuleString { get; set; }
    public string MethodName { get; set; }
  }

  [TestClass]
  public class MinidumpTests
  {
    [TestMethod]
    public void MinidumpCallstackTest()
    {
      using (var dt = TestTargets.NestedException.LoadMiniDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        var thread = runtime.GetMainThread();

        var frames = IntPtr.Size == 8 ? new[] {"Inner", "Inner", "Middle", "Outer", "Main"} : new[] {"Inner", "Middle", "Outer", "Main"};

        var i = 0;

        foreach (var frame in thread.StackTrace)
          if (frame.Kind == ClrStackFrameType.Runtime)
          {
            Assert.AreEqual(0ul, frame.InstructionPointer);
            Assert.AreNotEqual(0ul, frame.StackPointer);
          }
          else
          {
            Assert.AreNotEqual(0ul, frame.InstructionPointer);
            Assert.AreNotEqual(0ul, frame.StackPointer);
            Assert.IsNotNull(frame.Method);
            Assert.IsNotNull(frame.Method.Type);
            Assert.IsNotNull(frame.Method.Type.Module);
            Assert.AreEqual(frames[i++], frame.Method.Name);
          }
      }
    }

    [TestMethod]
    public void MinidumpExceptionPropertiesTest()
    {
      using (var dt = TestTargets.NestedException.LoadMiniDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();
        ExceptionTests.TestProperties(runtime);
      }
    }
  }
}