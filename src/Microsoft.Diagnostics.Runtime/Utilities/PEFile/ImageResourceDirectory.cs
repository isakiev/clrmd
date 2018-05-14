#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  internal struct IMAGE_RESOURCE_DIRECTORY
  {
    public int Characteristics;
    public int TimeDateStamp;
    public short MajorVersion;
    public short MinorVersion;
    public ushort NumberOfNamedEntries;
    public ushort NumberOfIdEntries;
    //  IMAGE_RESOURCE_DIRECTORY_ENTRY DirectoryEntries[];
  }
}