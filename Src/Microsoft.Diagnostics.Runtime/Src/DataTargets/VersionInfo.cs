using System;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  /// Represents the version of a DLL.
  /// </summary>
  [Serializable]
  public struct VersionInfo : IEquatable<VersionInfo>, IComparable<VersionInfo>
  {
    /// <summary>
    /// In a version 'A.B.C.D', this field represents 'A'.
    /// </summary>
    public int Major;

    /// <summary>
    /// In a version 'A.B.C.D', this field represents 'B'.
    /// </summary>
    public int Minor;

    /// <summary>
    /// In a version 'A.B.C.D', this field represents 'C'.
    /// </summary>
    public int Revision;

    /// <summary>
    /// In a version 'A.B.C.D', this field represents 'D'.
    /// </summary>
    public int Patch;

    internal VersionInfo(int major, int minor, int revision, int patch)
    {
      Major = major;
      Minor = minor;
      Revision = revision;
      Patch = patch;
    }

    public bool Equals(VersionInfo other)
    {
      return Major == other.Major && Minor == other.Minor && Revision == other.Revision && Patch == other.Patch;
    }

    public int CompareTo(VersionInfo other)
    {
      if (Major != other.Major)
        return Major.CompareTo(other.Major);

      if (Minor != other.Minor)
        return Minor.CompareTo(other.Minor);

      if (Revision != other.Revision)
        return Revision.CompareTo(other.Revision);

      return Patch.CompareTo(other.Patch);
    }

    public override string ToString()
    {
      return $"v{Major}.{Minor}.{Revision}.{Patch:D2}";
    }
  }
}