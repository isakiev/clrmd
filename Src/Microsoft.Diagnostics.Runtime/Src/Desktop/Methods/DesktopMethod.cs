using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopMethod : ClrMethod
  {
    private readonly uint _token;
    private ILToNativeMap[] _ilMap;
    private readonly string _sig;
    private readonly ulong _ip;
    private readonly ulong _gcInfo;
    private readonly MethodCompilationType _jit;
    private readonly MethodAttributes _attrs;
    private readonly DesktopRuntimeBase _runtime;
    private readonly DesktopHeapType _type;
    private List<ulong> _methodHandles;
    private ILInfo _il;
    private readonly HotColdRegions _hotColdInfo;
    
    internal static DesktopMethod Create(DesktopRuntimeBase runtime, IMetadataImport metadata, IMethodDescData mdData)
    {
      if (mdData == null)
        return null;

      MethodAttributes attrs = 0;
      if (metadata?.GetMethodProps(mdData.MDToken, out var pClass, null, 0, out var methodLength, out attrs, out var blob, out var blobLen, out var codeRva, out var implFlags) < 0)
        attrs = 0;

      return new DesktopMethod(runtime, mdData.MethodDesc, mdData, attrs);
    }

    internal void AddMethodHandle(ulong methodDesc)
    {
      if (_methodHandles == null)
        _methodHandles = new List<ulong>(1);

      _methodHandles.Add(methodDesc);
    }

    public override ulong MethodDesc
    {
      get
      {
        if (_methodHandles != null && _methodHandles[0] != 0)
          return _methodHandles[0];

        return EnumerateMethodDescs().FirstOrDefault();
      }
    }

    public override IEnumerable<ulong> EnumerateMethodDescs()
    {
      if (_methodHandles == null)
        _type?.InitMethodHandles();

      if (_methodHandles == null)
        _methodHandles = new List<ulong>();

      return _methodHandles;
    }

    internal static ClrMethod Create(DesktopRuntimeBase runtime, IMethodDescData mdData)
    {
      if (mdData == null)
        return null;

      var module = runtime.GetModule(mdData.Module);
      return Create(runtime, module?.GetMetadataImport(), mdData);
    }

    public DesktopMethod(DesktopRuntimeBase runtime, ulong md, IMethodDescData mdData, MethodAttributes attrs)
    {
      _runtime = runtime;
      _sig = runtime.GetNameForMD(md);
      _ip = mdData.NativeCodeAddr;
      _jit = mdData.JITType;
      _attrs = attrs;
      _token = mdData.MDToken;
      _gcInfo = mdData.GCInfo;
      var heap = runtime.Heap;
      _type = (DesktopHeapType)heap.GetTypeByMethodTable(mdData.MethodTable, 0);
      _hotColdInfo = new HotColdRegions {HotStart = _ip, HotSize = mdData.HotSize, ColdStart = mdData.ColdStart, ColdSize = mdData.ColdSize};
    }

    public override string Name
    {
      get
      {
        if (_sig == null)
          return null;

        var last = _sig.LastIndexOf('(');
        if (last > 0)
        {
          var first = _sig.LastIndexOf('.', last - 1);

          if (first != -1 && _sig[first - 1] == '.')
            first--;

          return _sig.Substring(first + 1, last - first - 1);
        }

        return "{error}";
      }
    }

    public override ulong NativeCode => _ip;

    public override MethodCompilationType CompilationType => _jit;

    public override string GetFullSignature()
    {
      return _sig;
    }

    public override int GetILOffset(ulong addr)
    {
      var map = ILOffsetMap;
      if (map == null)
        return -1;

      var ilOffset = 0;
      if (map.Length > 1)
        ilOffset = map[1].ILOffset;

      for (var i = 0; i < map.Length; ++i)
        if (map[i].StartAddress <= addr && addr <= map[i].EndAddress)
          return map[i].ILOffset;

      return ilOffset;
    }

    public override bool IsStatic => (_attrs & MethodAttributes.Static) == MethodAttributes.Static;

    public override bool IsFinal => (_attrs & MethodAttributes.Final) == MethodAttributes.Final;

    public override bool IsPInvoke => (_attrs & MethodAttributes.PinvokeImpl) == MethodAttributes.PinvokeImpl;

    public override bool IsVirtual => (_attrs & MethodAttributes.Virtual) == MethodAttributes.Virtual;

    public override bool IsAbstract => (_attrs & MethodAttributes.Abstract) == MethodAttributes.Abstract;

    public override bool IsPublic => (_attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;

    public override bool IsPrivate => (_attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;

    public override bool IsInternal
    {
      get
      {
        var access = _attrs & MethodAttributes.MemberAccessMask;
        return access == MethodAttributes.Assembly || access == MethodAttributes.FamANDAssem;
      }
    }

    public override bool IsProtected
    {
      get
      {
        var access = _attrs & MethodAttributes.MemberAccessMask;
        return access == MethodAttributes.Family || access == MethodAttributes.FamANDAssem || access == MethodAttributes.FamORAssem;
      }
    }

    public override bool IsSpecialName => (_attrs & MethodAttributes.SpecialName) == MethodAttributes.SpecialName;

    public override bool IsRTSpecialName => (_attrs & MethodAttributes.RTSpecialName) == MethodAttributes.RTSpecialName;

    public override HotColdRegions HotColdInfo => _hotColdInfo;

    public override ILToNativeMap[] ILOffsetMap
    {
      get
      {
        if (_ilMap == null)
          _ilMap = _runtime.GetILMap(_ip);

        return _ilMap;
      }
    }

    public override uint MetadataToken => _token;

    public override ClrType Type => _type;

    public override ulong GCInfo => _gcInfo;

    public override ILInfo IL
    {
      get
      {
        if (_il == null)
          InitILInfo();

        return _il;
      }
    }

    private void InitILInfo()
    {
      var module = Type?.Module;
      if (module?.MetadataImport is IMetadataImport metadataImport)
        if (metadataImport.GetRVA(_token, out var rva, out var flags) == 0)
        {
          var il = _runtime.GetILForModule(module, rva);
          if (il != 0)
          {
            _il = new ILInfo();

            if (_runtime.ReadByte(il, out byte b))
            {
              var isTinyHeader = (b & (ImageCorILMethod.FormatMask >> 1)) == ImageCorILMethod.TinyFormat;
              if (isTinyHeader)
              {
                _il.Address = il + 1;
                _il.Length = b >> (int)(ImageCorILMethod.FormatShift - 1);
                _il.LocalVarSignatureToken = ImageCorILMethod.mdSignatureNil;
              }
              else if (_runtime.ReadDword(il, out uint tmp))
              {
                _il.Flags = tmp;
                _runtime.ReadDword(il + 4, out tmp);
                _il.Length = (int)tmp;
                _runtime.ReadDword(il + 8, out tmp);
                _il.LocalVarSignatureToken = tmp;
                _il.Address = il + 12;
              }
            }
          }
        }
    }
    
    public override string ToString()
    {
      return _sig;
    }
  }
}