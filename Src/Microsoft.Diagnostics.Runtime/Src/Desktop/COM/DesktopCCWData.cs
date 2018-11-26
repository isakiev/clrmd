﻿using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopCCWData : CcwData
  {
    private readonly ulong _addr;
    private readonly ICCWData _ccw;
    private readonly DesktopGCHeap _heap;
    private List<ComInterfaceData> _interfaces;

    internal DesktopCCWData(DesktopGCHeap heap, ulong ccw, ICCWData data)
    {
      _addr = ccw;
      _ccw = data;
      _heap = heap;
    }

    public override ulong IUnknown => _ccw.IUnknown;
    public override ulong Object => _ccw.Object;
    public override ulong Handle => _ccw.Handle;
    public override int RefCount => _ccw.RefCount + _ccw.JupiterRefCount;

    public override IList<ComInterfaceData> Interfaces
    {
      get
      {
        if (_interfaces != null)
          return _interfaces;

        _heap.LoadAllTypes();

        _interfaces = new List<ComInterfaceData>();

        var interfaces = _heap.DesktopRuntime.GetCCWInterfaces(_addr, _ccw.InterfaceCount);
        for (var i = 0; i < interfaces.Length; ++i)
        {
          ClrType type = null;
          if (interfaces[i].MethodTable != 0)
            type = _heap.GetTypeByMethodTable(interfaces[i].MethodTable, 0);

          _interfaces.Add(new DesktopInterfaceData(type, interfaces[i].InterfacePointer));
        }

        return _interfaces;
      }
    }
  }
}