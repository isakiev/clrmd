#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct LegacyFieldData : IFieldData
  {
    private uint _type; // CorElementType
    private uint _sigType; // CorElementType
    private ulong _mtOfType; // NULL if Type is not loaded

    private ulong _moduleOfType;
    private uint _mdType;

    private uint _mdField;
    private ulong _MTOfEnclosingClass;
    private uint _dwOffset;
    private uint _bIsThreadLocal;
    private uint _bIsContextLocal;
    private uint _bIsStatic;
    private ulong _nextField;

    public uint CorElementType => _type;
    public uint SigType => _sigType;
    public ulong TypeMethodTable => _mtOfType;
    public ulong Module => _moduleOfType;
    public uint TypeToken => _mdType;
    public uint FieldToken => _mdField;
    public ulong EnclosingMethodTable => _MTOfEnclosingClass;
    public uint Offset => _dwOffset;
    public bool IsThreadLocal => _bIsThreadLocal != 0;
    bool IFieldData.IsContextLocal => _bIsContextLocal != 0;
    bool IFieldData.IsStatic => _bIsStatic != 0;
    ulong IFieldData.NextField => _nextField;
  }
}