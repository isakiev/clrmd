using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;
using Assert = Xunit.Assert;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  internal class StackTraceEntry
  {
    public ClrStackFrameType Kind { get; set; }
    public string ModuleString { get; set; }
    public string MethodName { get; set; }
  }

  public class MinidumpTests
  {
    [Fact]
    public void MinidumpCallstackTest()
    {
      using (var dt = TestTargets.NestedException.LoadMiniDump())
      {
        var runtime = dt.CreateSingleRuntime();
        var thread = runtime.GetMainThread();

        var frames = IntPtr.Size == 8 ? new[] {"Inner", "Inner", "Middle", "Outer", "Main"} : new[] {"Inner", "Middle", "Outer", "Main"};

        var i = 0;

        foreach (var frame in thread.StackTrace)
          if (frame.Kind == ClrStackFrameType.Runtime)
          {
            Assert.Equal(0ul, frame.InstructionPointer);
            Assert.NotEqual(0ul, frame.StackPointer);
          }
          else
          {
            Assert.NotEqual(0ul, frame.InstructionPointer);
            Assert.NotEqual(0ul, frame.StackPointer);
            Assert.NotNull(frame.Method);
            Assert.NotNull(frame.Method.Type);
            Assert.NotNull(frame.Method.Type.Module);
            Assert.Equal(frames[i++], frame.Method.Name);
          }
      }
    }

    [TestMethod]
    public void MinidumpExceptionPropertiesTest()
    {
      using (var dt = TestTargets.NestedException.LoadMiniDump())
      {
        var runtime = dt.CreateSingleRuntime();
        ExceptionTests.TestProperties(runtime);
      }
    }
  }
}