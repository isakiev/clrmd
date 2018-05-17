using System;

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45MethodTableData : IMethodTableData
  {
    public uint bIsFree; // everything else is NULL if this is true.
    public ulong module;
    public ulong eeClass;
    public ulong parentMethodTable;
    public ushort wNumInterfaces;
    public ushort wNumMethods;
    public ushort wNumVtableSlots;
    public ushort wNumVirtuals;
    public uint baseSize;
    public uint componentSize;
    public uint token;
    public uint dwAttrClass;
    public uint isShared; // flags & enum_flag_DomainNeutral
    public uint isDynamic;
    public uint containsPointers;

    public bool ContainsPointers => containsPointers != 0;
    public uint BaseSize => baseSize;
    public uint ComponentSize => componentSize;
    public ulong EEClass => eeClass;
    public bool Free => bIsFree != 0;
    public ulong Parent => parentMethodTable;
    public bool Shared => isShared != 0;
    public uint NumMethods => wNumMethods;
    public ulong ElementTypeHandle => throw new NotImplementedException();
  }
}