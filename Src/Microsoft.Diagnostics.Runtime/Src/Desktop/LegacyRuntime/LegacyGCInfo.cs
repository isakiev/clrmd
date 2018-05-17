#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct LegacyGCInfo : IGCInfo
  {
    public int serverMode;
    public int gcStructuresValid;
    public uint heapCount;
    public uint maxGeneration;

    bool IGCInfo.ServerMode => serverMode != 0;
    int IGCInfo.HeapCount => (int)heapCount;
    int IGCInfo.MaxGeneration => (int)maxGeneration;
    bool IGCInfo.GCStructuresValid => gcStructuresValid != 0;
  }
}