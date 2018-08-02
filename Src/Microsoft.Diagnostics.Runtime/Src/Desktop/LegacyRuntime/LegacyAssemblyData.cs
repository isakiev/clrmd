#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  // Same for both v2 and v4.
  internal struct LegacyAssemblyData : IAssemblyData
  {
    private ulong _assemblyPtr;
    private ulong _classLoader;
    private ulong _parentDomain;
    private ulong _appDomainPtr;
    private ulong _assemblySecDesc;
    private int _isDynamic;
    private int _moduleCount;
    private uint _loadContext;
    private int _isDomainNeutral;
    private uint _dwLocationFlags;

    public ulong Address => _assemblyPtr;

    public ulong ParentDomain => _parentDomain;

    public ulong AppDomain => _appDomainPtr;

    public bool IsDynamic => _isDynamic != 0;

    public bool IsDomainNeutral => _isDomainNeutral != 0;

    public int ModuleCount => _moduleCount;
  }
}