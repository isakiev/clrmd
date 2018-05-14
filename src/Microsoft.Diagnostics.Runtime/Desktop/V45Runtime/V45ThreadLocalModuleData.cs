#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45ThreadLocalModuleData
  {
    private ulong _threadAddr;
    private ulong _moduleIndex;

    private ulong _pClassData;
    private ulong _pDynamicClassTable;
    public ulong pGCStaticDataStart;
    public ulong pNonGCStaticDataStart;
  }
}