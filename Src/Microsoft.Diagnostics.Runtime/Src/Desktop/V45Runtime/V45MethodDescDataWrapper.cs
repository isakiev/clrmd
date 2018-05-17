namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class V45MethodDescDataWrapper : IMethodDescData
  {
    public bool Init(ISOSDac sos, ulong md)
    {
      var data = new V45MethodDescData();
      if (sos.GetMethodDescData(md, 0, out data, 0, null, out var count) < 0)
        return false;

      _md = data.MethodDescPtr;
      _ip = data.NativeCodeAddr;
      _module = data.ModulePtr;
      _token = data.MDToken;
      _mt = data.MethodTablePtr;

      if (sos.GetCodeHeaderData(data.NativeCodeAddr, out var header) >= 0)
      {
        if (header.JITType == 1)
          _jitType = MethodCompilationType.Jit;
        else if (header.JITType == 2)
          _jitType = MethodCompilationType.Ngen;
        else
          _jitType = MethodCompilationType.None;

        _gcInfo = header.GCInfo;
        _coldStart = header.ColdRegionStart;
        _coldSize = header.ColdRegionSize;
        _hotSize = header.HotRegionSize;
      }
      else
      {
        _jitType = MethodCompilationType.None;
      }

      return true;
    }

    private MethodCompilationType _jitType;
    private ulong _gcInfo, _md, _module, _ip, _coldStart;
    private uint _token, _coldSize, _hotSize;
    private ulong _mt;

    public ulong GCInfo => _gcInfo;
    public ulong MethodDesc => _md;
    public ulong Module => _module;
    public uint MDToken => _token;
    public ulong NativeCodeAddr => _ip;
    public MethodCompilationType JITType => _jitType;
    public ulong MethodTable => _mt;
    public ulong ColdStart => _coldStart;
    public uint ColdSize => _coldSize;
    public uint HotSize => _hotSize;
  }
}