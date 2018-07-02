using System.IO;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class SymbolLocatorTests
  {
    public static readonly string WellKnownDac = "mscordacwks_X86_X86_4.6.96.00.dll";
    public static readonly int WellKnownDacTimeStamp = 0x55b96946;
    public static readonly int WellKnownDacImageSize = 0x006a8000;

    internal static DefaultSymbolLocator GetLocator(string cacheLocation)
    {
      return new DefaultSymbolLocator(null, cacheLocation);
    }

    [TestMethod]
    public void SymbolLocatorTimeoutTest()
    {
      var locator = GetLocator(Path.Combine(Helpers.TestWorkingDirectory, "TestTimeout"));
      locator.Timeout = 10000;

      var dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
      Assert.IsNotNull(dac);
    }

    [TestMethod]
    public void FindBinaryNegativeTest()
    {
      var locator = GetLocator(Helpers.TestWorkingDirectory);
      var dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false);
      Assert.IsNull(dac);
    }

    [TestMethod]
    public void FindBinaryTest()
    {
      var locator = GetLocator(Helpers.TestWorkingDirectory);
      var dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
      Assert.IsNotNull(dac);
      Assert.IsTrue(File.Exists(dac));
    }
  }
}