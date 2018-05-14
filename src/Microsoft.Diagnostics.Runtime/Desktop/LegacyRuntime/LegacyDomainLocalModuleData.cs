using System;

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct LegacyDomainLocalModuleData : IDomainLocalModuleData
  {
    private ulong _appDomainAddr;
    private IntPtr _moduleID;

    private ulong _pClassData;
    private ulong _pDynamicClassTable;
    private ulong _pGCStaticDataStart;
    private ulong _pNonGCStaticDataStart;

    public ulong AppDomainAddr => _appDomainAddr;
    public ulong ModuleID => (ulong)_moduleID.ToInt64();
    public ulong ClassData => _pClassData;
    public ulong DynamicClassTable => _pDynamicClassTable;
    public ulong GCStaticDataStart => _pGCStaticDataStart;
    public ulong NonGCStaticDataStart => _pNonGCStaticDataStart;
  }
}