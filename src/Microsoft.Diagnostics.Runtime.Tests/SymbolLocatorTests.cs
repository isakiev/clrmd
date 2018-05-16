using System;
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

    internal static DefaultSymbolLocator GetLocator()
    {
      return new DefaultSymbolLocator {SymbolCache = Helpers.TestWorkingDirectory};
    }

    [TestMethod]
    public void SymbolLocatorTimeoutTest()
    {
      var locator = GetLocator();
      locator.Timeout = 10000;
      locator.SymbolCache += "\\TestTimeout";

      var dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
      Assert.IsNotNull(dac);
    }

    [TestMethod]
    public void FindBinaryNegativeTest()
    {
      var _locator = GetLocator();
      var dac = _locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false);
      Assert.IsNull(dac);
    }

    [TestMethod]
    public void FindBinaryTest()
    {
      var _locator = GetLocator();
      var dac = _locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
      Assert.IsNotNull(dac);
      Assert.IsTrue(File.Exists(dac));
    }

    private static bool PdbMatches(string pdb, Guid guid, int age)
    {
      PdbReader.GetPdbProperties(pdb, out var fileGuid, out var fileAge);

      return guid == fileGuid;
    }
  }
}