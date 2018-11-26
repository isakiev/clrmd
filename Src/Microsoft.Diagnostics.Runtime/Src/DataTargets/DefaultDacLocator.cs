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

    public string FindDac(ClrInfo clrInfo)
    {
      if (clrInfo == null) throw new ArgumentNullException(nameof(clrInfo));

      var moduleDirectory = Path.GetDirectoryName(clrInfo.ModuleInfo.FileName) ?? string.Empty;
      var dacLocation = Path.Combine(moduleDirectory, clrInfo.DacFileName);
      if (File.Exists(dacLocation))
        return dacLocation;

      return _symbolLocator.FindBinary(clrInfo.DacRequestFileName, (int)clrInfo.ModuleInfo.TimeStamp, (int)clrInfo.ModuleInfo.FileSize);
    }
  }
}