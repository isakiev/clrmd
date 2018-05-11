// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopInterfaceData : ComInterfaceData
  {
    public override ClrType Type => _type;

    public override ulong InterfacePointer => _interface;

    public DesktopInterfaceData(ClrType type, ulong ptr)
    {
      _type = type;
      _interface = ptr;
    }

    private readonly ulong _interface;
    private readonly ClrType _type;
  }

  internal class DesktopCCWData : CcwData
  {
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

          _interfaces.Add(new DesktopInterfaceData(type, interfaces[i].InterfacePtr));
        }

        return _interfaces;
      }
    }

    internal DesktopCCWData(DesktopGCHeap heap, ulong ccw, ICCWData data)
    {
      _addr = ccw;
      _ccw = data;
      _heap = heap;
    }

    private readonly ulong _addr;
    private readonly ICCWData _ccw;
    private readonly DesktopGCHeap _heap;
    private List<ComInterfaceData> _interfaces;
  }

  internal class DesktopRCWData : RcwData
  {
    //public ulong IdentityPointer { get; }
    public override ulong IUnknown => _rcw.UnknownPointer;
    public override ulong VTablePointer => _rcw.VTablePtr;
    public override int RefCount => _rcw.RefCount;
    public override ulong Object => _rcw.ManagedObject;
    public override bool Disconnected => _rcw.IsDisconnected;
    public override ulong WinRTObject => _rcw.JupiterObject;
    public override uint CreatorThread
    {
      get
      {
        if (_osThreadID == uint.MaxValue)
        {
          var data = _heap.DesktopRuntime.GetThread(_rcw.CreatorThread);
          if (data == null || data.OSThreadID == uint.MaxValue)
            _osThreadID = 0;
          else
            _osThreadID = data.OSThreadID;
        }

        return _osThreadID;
      }
    }

    public override IList<ComInterfaceData> Interfaces
    {
      get
      {
        if (_interfaces != null)
          return _interfaces;

        _heap.LoadAllTypes();

        _interfaces = new List<ComInterfaceData>();

        var interfaces = _heap.DesktopRuntime.GetRCWInterfaces(_addr, _rcw.InterfaceCount);
        for (var i = 0; i < interfaces.Length; ++i)
        {
          ClrType type = null;
          if (interfaces[i].MethodTable != 0)
            type = _heap.GetTypeByMethodTable(interfaces[i].MethodTable, 0);

          _interfaces.Add(new DesktopInterfaceData(type, interfaces[i].InterfacePtr));
        }

        return _interfaces;
      }
    }

    internal DesktopRCWData(DesktopGCHeap heap, ulong rcw, IRCWData data)
    {
      _addr = rcw;
      _rcw = data;
      _heap = heap;
      _osThreadID = uint.MaxValue;
    }

    private readonly IRCWData _rcw;
    private readonly DesktopGCHeap _heap;
    private uint _osThreadID;
    private List<ComInterfaceData> _interfaces;
    private readonly ulong _addr;
  }
}