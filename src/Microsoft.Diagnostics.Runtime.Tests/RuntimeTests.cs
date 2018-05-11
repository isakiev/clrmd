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
    [ExpectedException(typeof(InvalidOperationException))]
    public void CreationSpecificDacNegativeTest()
    {
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var badDac = dt.SymbolLocator.FindBinary(SymbolLocatorTests.WellKnownDac, SymbolLocatorTests.WellKnownDacTimeStamp, SymbolLocatorTests.WellKnownDacImageSize, false);

        Assert.IsNotNull(badDac);

        dt.ClrVersions.Single().CreateRuntime(badDac);

        if (dt.ClrVersions.Single().DacInfo.FileName.Equals(SymbolLocatorTests.WellKnownDac, StringComparison.OrdinalIgnoreCase))
          Assert.Inconclusive();
      }
    }

    [TestMethod]
    public void CreationSpecificDac()
    {
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var info = dt.ClrVersions.Single();
        var dac = info.LocalMatchingDac;

        Assert.IsNotNull(dac);

        var runtime = info.CreateRuntime(dac);
        Assert.IsNotNull(runtime);
      }
    }

    [TestMethod]
    public void RuntimeClrInfo()
    {
      using (var dt = TestTargets.NestedException.LoadFullDump())
      {
        var info = dt.ClrVersions.Single();
        var runtime = info.CreateRuntime();

        Assert.AreEqual(info, runtime.ClrInfo);
      }
    }

    [TestMethod]
    public void ModuleEnumerationTest()
    {
      // This test ensures that we enumerate all modules in the process exactly once.

      using (var dt = TestTargets.AppDomains.LoadFullDump())
      {
        var runtime = dt.ClrVersions.Single().CreateRuntime();

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