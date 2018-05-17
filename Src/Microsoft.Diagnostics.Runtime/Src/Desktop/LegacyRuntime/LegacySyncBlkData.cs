#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct LegacySyncBlkData : ISyncBlkData
  {
    private ulong _pObject;
    private uint _bFree;
    private ulong _syncBlockPointer;
    private uint _COMFlags;
    private uint _bMonitorHeld;
    private uint _nRecursion;
    private ulong _holdingThread;
    private uint _additionalThreadCount;
    private ulong _appDomainPtr;
    private uint _syncBlockCount;

    public bool Free => _bFree != 0;

    public ulong Object => _pObject;

    public bool MonitorHeld => _bMonitorHeld != 0;

    public uint Recursion => _nRecursion;

    public uint TotalCount => _syncBlockCount;

    public ulong OwningThread => _holdingThread;

    public ulong Address => _syncBlockPointer;
  }
}