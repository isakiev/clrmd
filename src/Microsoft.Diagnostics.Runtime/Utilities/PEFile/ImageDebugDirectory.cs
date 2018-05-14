#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  internal struct ImageDebugDirectory
  {
    public int Characteristics;
    public int TimeDateStamp;
    public short MajorVersion;
    public short MinorVersion;
    public ImageDebugType Type;
    public int SizeOfData;
    public int AddressOfRawData;
    public int PointerToRawData;
  }
}