#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45DomainLocalModuleData : IDomainLocalModuleData
  {
    private ulong _appDomainAddr;
    private ulong _moduleID;

    private ulong _pClassData;
    private ulong _pDynamicClassTable;
    private ulong _pGCStaticDataStart;
    private ulong _pNonGCStaticDataStart;

    public ulong AppDomainAddr => _appDomainAddr;
    public ulong ModuleID => _moduleID;
    public ulong ClassData => _pClassData;
    public ulong DynamicClassTable => _pDynamicClassTable;
    public ulong GCStaticDataStart => _pGCStaticDataStart;
    public ulong NonGCStaticDataStart => _pNonGCStaticDataStart;
  }
}