using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   Represents information about a single Clr runtime in a process.
  /// </summary>
  [Serializable]
  public class ClrInfo : IComparable<ClrInfo>, IComparable
  {
    private const string DesktopModuleName1 = "clr";
    private const string DesktopModuleName2 = "mscorwks";
    private const string CoreModuleName = "coreclr";
    private const string NativeModuleName = "mrt100_app";

    /// <summary>
    ///   The type of CLR this module represents.
    /// </summary>
    public ClrFlavor Flavor { get; }

    /// <summary>
    ///   Returns module information about the ClrInstance.
    /// </summary>
    public ModuleInfo ModuleInfo { get; }

    /// <summary>
    ///   The version number of this runtime.
    /// </summary>
    public VersionInfo Version => ModuleInfo.Version;

    private ClrInfo(ClrFlavor flavor, ModuleInfo moduleInfo)
    {
      Flavor = flavor;
      ModuleInfo = moduleInfo;
    }

    public static bool IsClrRuntime(ModuleInfo moduleInfo, out ClrInfo clrInfo)
    {
      clrInfo = null;

      var moduleName = Path.GetFileNameWithoutExtension(moduleInfo.FileName);
      if (moduleName == null)
        return false;

      moduleName = moduleName.ToLower();

      ClrFlavor flavor;
      switch (moduleName)
      {
        case DesktopModuleName1:
        case DesktopModuleName2:
          flavor = ClrFlavor.Desktop;
          break;

        case CoreModuleName:
          flavor = ClrFlavor.Core;
          break;

        case NativeModuleName:
          flavor = ClrFlavor.Native;
          break;

        default:
          return false;
      }

      clrInfo = new ClrInfo(flavor, moduleInfo);
      return true;
    }

    public override string ToString()
    {
      return Version.ToString();
    }

    public int CompareTo(ClrInfo other)
    {
      if (ReferenceEquals(this, other)) return 0;
      if (ReferenceEquals(null, other)) return 1;

      if (Flavor != other.Flavor)
        return Flavor.CompareTo(other.Flavor);

      return Version.CompareTo(other.Version);
    }

    public int CompareTo(object obj)
    {
      if (ReferenceEquals(this, obj)) return 0;
      if (ReferenceEquals(null, obj)) return 1;

      return CompareTo(obj as ClrInfo);
    }
  }
}