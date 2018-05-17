#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  // Same for both v2 and v4.
  internal struct LegacyAppDomainData : IAppDomainData
  {
    private ulong _address;
    private ulong _appSecDesc;
    private ulong _pLowFrequencyHeap;
    private ulong _pHighFrequencyHeap;
    private ulong _pStubHeap;
    private ulong _pDomainLocalBlock;
    private ulong _pDomainLocalModules;
    private int _dwId;
    private int _assemblyCount;
    private int _failedAssemblyCount;
    private int _appDomainStage;

    public int Id => _dwId;
    public ulong Address => _address;
    public ulong LowFrequencyHeap => _pLowFrequencyHeap;
    public ulong HighFrequencyHeap => _pHighFrequencyHeap;
    public ulong StubHeap => _pStubHeap;
    public int AssemblyCount => _assemblyCount;
  }
}