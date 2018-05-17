#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45MethodDescData
  {
    private uint _bHasNativeCode;
    private uint _bIsDynamic;
    private short _wSlotNumber;
    internal ulong NativeCodeAddr;
    // Useful for breaking when a method is jitted.
    private ulong _addressOfNativeCodeSlot;

    internal ulong MethodDescPtr;
    internal ulong MethodTablePtr;
    internal ulong ModulePtr;

    internal uint MDToken;
    public ulong GCInfo;
    private ulong _GCStressCodeCopy;

    // This is only valid if bIsDynamic is true
    private ulong _managedDynamicMethodObject;

    private ulong _requestedIP;

    // Gives info for the single currently active version of a method
    private V45ReJitData _rejitDataCurrent;

    // Gives info corresponding to requestedIP (for !ip2md)
    private V45ReJitData _rejitDataRequested;

    // Total number of rejit versions that have been jitted
    private uint _cJittedRejitVersions;
  }
}