#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V35MethodDescData : IMethodDescData
  {
    private int _bHasNativeCode;
    private int _bIsDynamic;
    private short _wSlotNumber;
    private ulong _nativeCodeAddr;
    // Useful for breaking when a method is jitted.
    private ulong _addressOfNativeCodeSlot;

    private ulong _methodDescPtr;
    private ulong _methodTablePtr;
    private ulong _EEClassPtr;
    private ulong _modulePtr;

    private uint _mdToken;
    private ulong _GCInfo;
    private short _JITType;
    private ulong _GCStressCodeCopy;

    // This is only valid if bIsDynamic is true
    private ulong _managedDynamicMethodObject;
    
    public ulong MethodTable => _methodTablePtr;
    public ulong MethodDesc => _methodDescPtr;
    public ulong Module => _modulePtr;
    public uint MDToken => _mdToken;
    public ulong GCInfo => _GCInfo;
    ulong IMethodDescData.NativeCodeAddr => _nativeCodeAddr;

    MethodCompilationType IMethodDescData.JITType
    {
      get
      {
        if (_JITType == 1)
          return MethodCompilationType.Jit;
        if (_JITType == 2)
          return MethodCompilationType.Ngen;

        return MethodCompilationType.None;
      }
    }

    public ulong ColdStart => 0;
    public uint ColdSize => 0;
    public uint HotSize => 0;
  }
}