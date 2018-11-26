using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
  public static class ClrInfoProvider
  {
    private const string DesktopModuleName1 = "clr";
    private const string DesktopModuleName2 = "mscorwks";
    private const string WindowsCoreModuleName = "coreclr";
    private const string LinuxCoreModuleName = "libcoreclr"; //TODO
    private const string NativeModuleName = "mrt100_app";

    private static bool TryGetModuleName(ModuleInfo moduleInfo, out string moduleName)
    {
      moduleName = Path.GetFileNameWithoutExtension(moduleInfo.FileName);
      if (moduleName == null)
        return false;

      moduleName = moduleName.ToLower();
      return true;
    }

    public static bool IsSupportedRuntime(ModuleInfo moduleInfo, out ClrFlavor flavor)
    {
      flavor = default;

      if (!TryGetModuleName(moduleInfo, out var moduleName))
        return false;

      switch (moduleName)
      {
        case DesktopModuleName1:
        case DesktopModuleName2:
          flavor = ClrFlavor.Desktop;
          return true;

        case WindowsCoreModuleName:
          flavor = ClrFlavor.Core;
          return true;

        default:
          return false;
      }
    }

    public static bool IsNativeRuntime(ModuleInfo moduleInfo)
    {
      if (!TryGetModuleName(moduleInfo, out var moduleName))
        return false;

      return moduleName == NativeModuleName;
    }

    public static string GetDacFileName(ClrFlavor flavor)
    {
      //TODO: .so in case of Linux
      return flavor == ClrFlavor.Core ? "mscordaccore.dll" : "mscordacwks.dll";
    }

    public static string GetDacRequestFileName(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, VersionInfo clrVersion)
    {
      var dacName = flavor == ClrFlavor.Core ? "mscordaccore" : "mscordacwks";
      return $"{dacName}_{currentArchitecture}_{targetArchitecture}_{clrVersion.Major}.{clrVersion.Minor}.{clrVersion.Revision}.{clrVersion.Patch:D2}.dll";
    }
  }
}