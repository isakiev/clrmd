#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  // Same for both v2 and v4.
  internal struct LegacyAppDomainStoreData : IAppDomainStoreData
  {
    private ulong _shared;
    private ulong _system;
    private int _domainCount;

    public ulong SharedDomain => _shared;
    public ulong SystemDomain => _system;
    public int Count => _domainCount;
  }
}