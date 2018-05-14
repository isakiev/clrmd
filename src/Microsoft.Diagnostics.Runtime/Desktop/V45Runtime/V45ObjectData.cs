#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45ObjectData : IObjectData
  {
    private ulong _methodTable;
    private uint _objectType;
    private ulong _size;
    private ulong _elementTypeHandle;
    private uint _elementType;
    private uint _dwRank;
    private ulong _dwNumComponents;
    private ulong _dwComponentSize;
    private ulong _arrayDataPtr;
    private ulong _arrayBoundsPtr;
    private ulong _arrayLowerBoundsPtr;

    private ulong _rcw;
    private ulong _ccw;

    public ClrElementType ElementType => (ClrElementType)_elementType;
    public ulong ElementTypeHandle => _elementTypeHandle;
    public ulong RCW => _rcw;
    public ulong CCW => _ccw;

    public ulong DataPointer => _arrayDataPtr;
  }
}