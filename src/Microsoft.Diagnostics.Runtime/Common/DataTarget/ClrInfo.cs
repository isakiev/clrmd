using System;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   Represents information about a single Clr runtime in a process.
  /// </summary>
  [Serializable]
  public class ClrInfo : IComparable
  {
    private const string DesktopModuleName1 = "clr";
    private const string DesktopModuleName2 = "mscorwks";
    private const string CoreModuleName = "coreclr";
    private const string NativeModuleName = "mrt100_app";

    private static string GetLowerCaseName(ModuleInfo module)
    {
      var fileName = Path.GetFileNameWithoutExtension(module.FileName);
      return fileName?.ToLower();
    }
    
    public static bool IsClrModule(ModuleInfo module)
    {
      if (module == null) throw new ArgumentNullException(nameof(module));

      var moduleName = GetLowerCaseName(module);
      return moduleName == DesktopModuleName1 || moduleName == DesktopModuleName2 || moduleName == CoreModuleName || moduleName == NativeModuleName;
    }
    
    internal ClrInfo(ModuleInfo module, Architecture architecture, IDataReader dataReader)
    {
      if (module == null) throw new ArgumentNullException(nameof(module));
      
      var moduleName = GetLowerCaseName(module);
      switch (moduleName)
      {
        case DesktopModuleName1:
        case DesktopModuleName2:
          Flavor = ClrFlavor.Desktop;
          break;
          
        case CoreModuleName:
          Flavor = ClrFlavor.Core;
          break;
        
        case NativeModuleName:
          Flavor = ClrFlavor.Native;
          break;

        default:
          throw new ClrDiagnosticsException("Specified module is not recognized as a CLR one");
      }
      
      var moduleDirectory = Path.GetDirectoryName(module.FileName) ?? string.Empty;
      DacLocation = Path.Combine(moduleDirectory, DacInfo.GetDacFileName(Flavor, architecture));
      if (!File.Exists(DacLocation) || !NativeMethods.IsEqualFileVersion(DacLocation, module.Version))
        DacLocation = null;

      var version = module.Version;
      var dacAgnosticName = DacInfo.GetDacRequestFileName(Flavor, architecture, architecture, version);
      var dacFileName = DacInfo.GetDacRequestFileName(Flavor, IntPtr.Size == 4 ? Architecture.X86 : Architecture.Amd64, architecture, version);

      DacInfo = new DacInfo(dataReader, dacAgnosticName, architecture)
      {
        FileSize = module.FileSize,
        TimeStamp = module.TimeStamp,
        FileName = dacFileName,
        Version = module.Version
      };
      
      ModuleInfo = module;
    }
    
    /// <summary>
    ///   The version number of this runtime.
    /// </summary>
    public VersionInfo Version => ModuleInfo.Version;
    
    /// <summary>
    ///   The type of CLR this module represents.
    /// </summary>
    public ClrFlavor Flavor { get; }
    
    /// <summary>
    ///   Returns module information about the Dac needed create a ClrRuntime instance for this runtime.
    /// </summary>
    public DacInfo DacInfo { get; }

    /// <summary>
    ///   Returns module information about the ClrInstance.
    /// </summary>
    public ModuleInfo ModuleInfo { get; }

    /// <summary>
    ///   Returns the location of the local dac on your machine which matches this version of Clr, or null
    ///   if one could not be found.
    /// </summary>
    public string DacLocation { get; }
    
    public override string ToString()
    {
      return Version.ToString();
    }

    /// <summary>
    ///   IComparable. Sorts the object by version.
    /// </summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns>-1 if less, 0 if equal, 1 if greater.</returns>
    public int CompareTo(object obj)
    {
      if (obj == null)
        return 1;

      if (!(obj is ClrInfo))
        throw new InvalidOperationException("Object not ClrInfo.");

      var flv = ((ClrInfo)obj).Flavor;
      if (flv != Flavor)
        return flv.CompareTo(Flavor); // Intentionally reversed.

      var rhs = ((ClrInfo)obj).Version;
      if (Version.Major != rhs.Major)
        return Version.Major.CompareTo(rhs.Major);

      if (Version.Minor != rhs.Minor)
        return Version.Minor.CompareTo(rhs.Minor);

      if (Version.Revision != rhs.Revision)
        return Version.Revision.CompareTo(rhs.Revision);

      return Version.Patch.CompareTo(rhs.Patch);
    }
  }
}