using System;
using System.IO;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
  public class DefaultDacLocator : IDacLocator
  {
    private readonly ISymbolLocator _symbolLocator;

    public DefaultDacLocator(ISymbolLocator symbolLocator)
    {
      _symbolLocator = symbolLocator ?? throw new ArgumentNullException(nameof(symbolLocator));
    }

    public string FindDac(ClrInfo clrInfo, Architecture architecture)
    {
      if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));

      var moduleDirectory = Path.GetDirectoryName(clrInfo.ModuleInfo.FileName) ?? string.Empty;
      var dacFileName = DacInfo.GetDacFileName(clrInfo.Flavor, architecture);

      var dacLocation = Path.Combine(moduleDirectory, dacFileName);
      if (File.Exists(dacLocation))
        return dacLocation;
      
      var dacRequestFileName = DacInfo.GetDacRequestFileName(clrInfo.Flavor, architecture, architecture, clrInfo.Version);
      return _symbolLocator.FindBinary(dacRequestFileName, (int)clrInfo.ModuleInfo.TimeStamp, (int)clrInfo.ModuleInfo.FileSize);
    }
  }
}