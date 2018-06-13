using System.Runtime.InteropServices;

// ReSharper disable MemberCanBePrivate.Global
namespace Microsoft.Diagnostics.Runtime.DataReaders.Simple
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct DumpSystemInfo
  {
    // These 3 fields are the same as in the SYSTEM_INFO structure from GetSystemInfo().
    // As of .NET 2.0, there is no existing managed object that represents these.
    public readonly DumpProcessorArchitecture ProcessorArchitecture;
    public readonly ushort ProcessorLevel; // only used for display purposes
    public readonly ushort ProcessorRevision;

    public readonly byte NumberOfProcessors;
    public readonly byte ProductType;

    // These next 4 fields plus CSDVersionRva are the same as the OSVERSIONINFO structure from GetVersionEx().
    // This can be represented as a System.Version.
    public readonly uint MajorVersion;
    public readonly uint MinorVersion;
    public readonly uint BuildNumber;

    // This enum is the same value as System.PlatformId.
    public readonly int PlatformId;

    // RVA to a CSDVersion string in the string table.
    // This would be a string like "Service Pack 1".
    public readonly uint CSDVersionRva;
  }
}