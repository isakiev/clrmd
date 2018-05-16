using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class RuntimeTests
  {
    [TestMethod]
    public void RuntimeClrInfo()
    {
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var info = dt.ClrVersions.Single();
        var runtime = dt.CreateRuntime(info);

        Assert.AreEqual(info, runtime.ClrInfo);
      }
    }

    [TestMethod]
    public void ModuleEnumerationTest()
    {
      // This test ensures that we enumerate all modules in the process exactly once.

      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.CreateSingleRuntime();

        var expected = new HashSet<string>(new[] {"mscorlib.dll", "sharedlibrary.dll", "nestedexception.exe", "appdomains.exe"}, StringComparer.OrdinalIgnoreCase);
        var modules = new HashSet<ClrModule>();

        foreach (var module in runtime.Modules)
        {
          Assert.IsTrue(expected.Contains(Path.GetFileName(module.FileName)));
          Assert.IsFalse(modules.Contains(module));
          modules.Add(module);
        }
      }
    }
  }
}