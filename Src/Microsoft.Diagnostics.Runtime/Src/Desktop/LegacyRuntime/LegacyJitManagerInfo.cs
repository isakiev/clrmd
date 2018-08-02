#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  // Same for v2 and v4
  internal struct LegacyJitManagerInfo
  {
    public ulong addr;
    public CodeHeapType type;
    public ulong ptrHeapList;
  }
}