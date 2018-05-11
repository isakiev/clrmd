﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class SymbolLocatorTests
  {
    public static readonly string WellKnownDac = "mscordacwks_X86_X86_4.6.96.00.dll";
    public static readonly int WellKnownDacTimeStamp = 0x55b96946;
    public static readonly int WellKnownDacImageSize = 0x006a8000;

    public static readonly string WellKnownNativePdb = "clr.pdb";
    public static readonly Guid WellKnownNativePdbGuid = new Guid("0350aa66-2d49-4425-ab28-9b43a749638d");
    public static readonly int WellKnownNativePdbAge = 2;

    internal static SymbolLocator GetLocator()
    {
      return new DefaultSymbolLocator {SymbolCache = Helpers.TestWorkingDirectory};
    }

    [TestMethod]
    public void SymbolLocatorTimeoutTest()
    {
      var locator = GetLocator();
      locator.Timeout = 10000;
      locator.SymbolCache += "\\TestTimeout";

      var pdb = locator.FindPdb(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge);
      Assert.IsNotNull(pdb);
    }

    [TestMethod]
    public void FindBinaryNegativeTest()
    {
      var _locator = GetLocator();
      var dac = _locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false);
      Assert.IsNull(dac);
    }

    [TestMethod]
    public void FindPdbNegativeTest()
    {
      var _locator = GetLocator();
      var pdb = _locator.FindPdb(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge + 1);
      Assert.IsNull(pdb);
    }

    [TestMethod]
    public async Task FindBinaryAsyncNegativeTest()
    {
      var _locator = GetLocator();

      var tasks = new List<Task<string>>();
      for (var i = 0; i < 10; i++)
        tasks.Add(_locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false));

      // Ensure we got the same answer for everything.
      foreach (var task in tasks)
      {
        var dac = await task;
        Assert.IsNull(dac);
      }
    }

    [TestMethod]
    public async Task FindPdbAsyncNegativeTest()
    {
      var _locator = GetLocator();

      var tasks = new List<Task<string>>();
      for (var i = 0; i < 10; i++)
        tasks.Add(_locator.FindPdbAsync(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge + 1));

      // Ensure we got the same answer for everything.
      foreach (var task in tasks)
      {
        var pdb = await task;
        Assert.IsNull(pdb);
      }
    }

    [TestMethod]
    public void FindBinaryTest()
    {
      var _locator = GetLocator();
      var dac = _locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
      Assert.IsNotNull(dac);
      Assert.IsTrue(File.Exists(dac));
    }

    [TestMethod]
    public void FindPdbTest()
    {
      var _locator = GetLocator();
      var pdb = _locator.FindPdb(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge);
      Assert.IsNotNull(pdb);
      Assert.IsTrue(File.Exists(pdb));

      Assert.IsTrue(PdbMatches(pdb, WellKnownNativePdbGuid, WellKnownNativePdbAge));
    }

    private static bool PdbMatches(string pdb, Guid guid, int age)
    {
      PdbReader.GetPdbProperties(pdb, out var fileGuid, out var fileAge);

      return guid == fileGuid;
    }

    [TestMethod]
    public async Task FindBinaryAsyncTest()
    {
      var _locator = GetLocator();
      var first = _locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);

      var tasks = new List<Task<string>>();
      for (var i = 0; i < 10; i++)
        tasks.Add(_locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false));

      var dac = await first;

      Assert.IsNotNull(dac);
      Assert.IsTrue(File.Exists(dac));
      new PEFile(dac).Dispose(); // This will throw if the image is invalid.

      // Ensure we got the same answer for everything.
      foreach (var task in tasks)
      {
        var taskDac = await task;
        Assert.AreEqual(dac, taskDac);
      }
    }

    [TestMethod]
    public async Task FindPdbAsyncTest()
    {
      var _locator = GetLocator();
      var first = _locator.FindPdbAsync(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge);

      var tasks = new List<Task<string>>();
      for (var i = 0; i < 10; i++)
        tasks.Add(_locator.FindPdbAsync(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge));

      var pdb = await first;

      Assert.IsNotNull(pdb);
      Assert.IsTrue(File.Exists(pdb));
      Assert.IsTrue(PdbMatches(pdb, WellKnownNativePdbGuid, WellKnownNativePdbAge));

      // Ensure we got the same answer for everything.
      foreach (var task in tasks)
      {
        var taskPdb = await task;
        Assert.AreEqual(taskPdb, pdb);
      }
    }
  }
}