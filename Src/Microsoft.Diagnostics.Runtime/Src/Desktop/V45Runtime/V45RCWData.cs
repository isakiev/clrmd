#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45RCWData : IRCWData
  {
    private ulong _identityPointer;
    private ulong _unknownPointer;
    private ulong _managedObject;
    private ulong _jupiterObject;
    private ulong _vtablePtr;
    private ulong _creatorThread;
    private ulong _ctxCookie;

    private int _refCount;
    private int _interfaceCount;

    private uint _isJupiterObject;
    private uint _supportsIInspectable;
    private uint _isAggregated;
    private uint _isContained;
    private uint _isFreeThreaded;
    private uint _isDisconnected;

    public ulong IdentityPointer => _identityPointer;
    public ulong UnknownPointer => _unknownPointer;
    public ulong ManagedObject => _managedObject;
    public ulong JupiterObject => _jupiterObject;
    public ulong VTablePtr => _vtablePtr;
    public ulong CreatorThread => _creatorThread;
    public int RefCount => _refCount;
    public int InterfaceCount => _interfaceCount;
    public bool IsJupiterObject => _isJupiterObject != 0;
    public bool IsDisconnected => _isDisconnected != 0;
  }
}