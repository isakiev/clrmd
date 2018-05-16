namespace Microsoft.Diagnostics.Runtime
{
  public static class DacInfo
  {
    /// <summary>
    ///   Returns the filename of the dac dll according to the specified parameters
    /// </summary>
    public static string GetDacRequestFileName(ClrFlavor flavor, Architecture currentArchitecture, Architecture targetArchitecture, VersionInfo clrVersion)
    {
      if (flavor == ClrFlavor.Native)
        return targetArchitecture == Architecture.Amd64 ? "mrt100dac_winamd64.dll" : "mrt100dac_winx86.dll";

      var dacName = flavor == ClrFlavor.Core ? "mscordaccore" : "mscordacwks";
      return string.Format(
        "{0}_{1}_{2}_{3}.{4}.{5}.{6:D2}.dll",
        dacName,
        currentArchitecture,
        targetArchitecture,
        clrVersion.Major,
        clrVersion.Minor,
        clrVersion.Revision,
        clrVersion.Patch);
    }

    internal static string GetDacFileName(ClrFlavor flavor, Architecture targetArchitecture)
    {
      if (flavor == ClrFlavor.Native)
        return targetArchitecture == Architecture.Amd64 ? "mrt100dac_winamd64.dll" : "mrt100dac_winx86.dll";

      return flavor == ClrFlavor.Core ? "mscordaccore.dll" : "mscordacwks.dll";
    }
  }
}