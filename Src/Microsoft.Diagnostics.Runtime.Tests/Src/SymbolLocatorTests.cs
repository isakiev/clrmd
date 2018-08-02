using System.IO;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Diagnostics.Runtime.Tests
{
  [TestClass]
  public class SymbolLocatorTests
  {
    private static readonly string WellKnownDac = "mscordacwks_X86_X86_4.6.96.00.dll";
    private static readonly int WellKnownDacTimeStamp = 0x55b96946;
    private static readonly int WellKnownDacImageSize = 0x006a8000;

    private static DefaultSymbolLocator GetLocator()
    {
      var cacheLocation = Path.Combine(Helpers.GetTempPath(), "Cache");
      Directory.CreateDirectory(cacheLocation);
      return new DefaultSymbolLocator(DefaultLogger.Instance, cacheLocation);
    }

    [TestMethod]
    public void TestSymbolLocatorTimeout()
    {
      var locator = GetLocator();
      locator.Timeout = 10000;
      var dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
      Assert.IsNotNull(dac);
    }

    [TestMethod]
    public void TestNegativeFindBinary()
    {
      var locator = GetLocator();
      var dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false);
      Assert.IsNull(dac);
    }

    [TestMethod]
    public void TestFindBinary()
    {
      var locator = GetLocator();
      var dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
      Assert.IsNotNull(dac);
      Assert.IsTrue(File.Exists(dac));
    }
  }
}