using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   Represents information about a single Clr runtime in a process.
  /// </summary>
  [Serializable]
  public class ClrInfo
  {
    public ClrFlavor Flavor { get; }
    public ModuleInfo ModuleInfo { get; }
    public Architecture Architecture { get; }

    public ClrInfo(ClrFlavor flavor, ModuleInfo moduleInfo, Architecture architecture)
    {
      Flavor = flavor;
      ModuleInfo = moduleInfo;
      Architecture = architecture;
    }
    
    public VersionInfo Version => ModuleInfo.Version;
    public string DacFileName => ClrInfoProvider.GetDacFileName(Flavor);
    public string DacRequestFileName => ClrInfoProvider.GetDacRequestFileName(Flavor, Architecture, Architecture, Version);

    public override string ToString()
    {
      return $"{Flavor} {Version} {Architecture}";
    }
  }
}