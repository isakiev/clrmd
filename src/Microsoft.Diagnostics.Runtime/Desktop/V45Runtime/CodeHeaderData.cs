#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct CodeHeaderData
  {
    public ulong GCInfo;
    public uint JITType;
    public ulong MethodDescPtr;
    public ulong MethodStart;
    public uint MethodSize;
    public ulong ColdRegionStart;
    public uint ColdRegionSize;
    public uint HotRegionSize;
  }
}