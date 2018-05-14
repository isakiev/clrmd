#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45CCWData : ICCWData
  {
    private ulong _outerIUnknown;
    private ulong _managedObject;
    private ulong _handle;
    private ulong _ccwAddress;

    private int _refCount;
    private int _interfaceCount;
    private uint _isNeutered;

    private int _jupiterRefCount;
    private uint _isPegged;
    private uint _isGlobalPegged;
    private uint _hasStrongRef;
    private uint _isExtendsCOMObject;
    private uint _hasWeakReference;
    private uint _isAggregated;

    public ulong IUnknown => _outerIUnknown;

    public ulong Object => _managedObject;

    public ulong Handle => _handle;

    public ulong CCWAddress => _ccwAddress;

    public int RefCount => _refCount;

    public int JupiterRefCount => _jupiterRefCount;

    public int InterfaceCount => _interfaceCount;
  }
}