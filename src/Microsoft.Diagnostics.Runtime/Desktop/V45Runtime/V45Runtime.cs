using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class V45Runtime : DesktopRuntimeBase
  {
    private ISOSDac _sos;

    public V45Runtime(ClrInfo info, DataTarget dt, DacLibrary lib)
      : base(info, dt, lib)
    {
      if (!GetCommonMethodTables(ref _commonMTs))
        throw new ClrDiagnosticsException("Could not request common MethodTable list.", ClrDiagnosticsException.HR.DacError);

      if (!_commonMTs.Validate())
        CanWalkHeap = false;

      // Ensure the version of the dac API matches the one we expect.  (Same for both
      // v2 and v4 rtm.)
      var tmp = new byte[sizeof(int)];

      if (!Request(DacRequests.VERSION, null, tmp))
        throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsException.HR.DacError);

      var v = BitConverter.ToInt32(tmp, 0);
      if (v != 9)
        throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsException.HR.DacError);
    }

    protected override void InitApi()
    {
      if (_sos == null)
        _sos = _library.SOSInterface;

      Debug.Assert(_sos != null);
    }

    internal override DesktopVersion CLRVersion => DesktopVersion.v45;

    private ISOSHandleEnum _handleEnum;
    private List<ClrHandle> _handles;

    public override IEnumerable<ClrHandle> EnumerateHandles()
    {
      if (_handles != null && _handleEnum == null)
        return _handles;

      return EnumerateHandleWorker();
    }

    internal override Dictionary<ulong, List<ulong>> GetDependentHandleMap(CancellationToken cancelToken)
    {
      var result = new Dictionary<ulong, List<ulong>>();

      if (_sos.GetHandleEnum(out var tmp) < 0)
        return result;

      var enumerator = (ISOSHandleEnum)tmp;
      var handles = new HandleData[32];
      uint fetched = 0;
      do
      {
        if (enumerator.Next((uint)handles.Length, handles, out fetched) < 0 || fetched <= 0)
          break;

        for (var i = 0; i < fetched; i++)
        {
          cancelToken.ThrowIfCancellationRequested();

          var type = (HandleType)handles[i].Type;
          if (type != HandleType.Dependent)
            continue;

          if (ReadPointer(handles[i].Handle, out var address))
          {
            if (!result.TryGetValue(address, out var value))
              result[address] = value = new List<ulong>();

            value.Add(handles[i].Secondary);
          }
        }
      } while (fetched > 0);

      return result;
    }

    private IEnumerable<ClrHandle> EnumerateHandleWorker()
    {
      // handles was fully populated already
      if (_handles != null && _handleEnum == null)
        yield break;

      // Create _handleEnum if it's not already created.
      if (_handleEnum == null)
      {
        if (_sos.GetHandleEnum(out var tmp) < 0)
          yield break;

        _handleEnum = tmp as ISOSHandleEnum;
        if (_handleEnum == null)
          yield break;

        _handles = new List<ClrHandle>();
      }

      // We already partially enumerated handles before, start with them.
      foreach (var handle in _handles)
        yield return handle;

      var handles = new HandleData[8];
      uint fetched = 0;
      do
      {
        if (_handleEnum.Next((uint)handles.Length, handles, out fetched) < 0 || fetched <= 0)
          break;

        var curr = _handles.Count;
        for (var i = 0; i < fetched; i++)
        {
          var handle = new ClrHandle(this, Heap, handles[i]);
          _handles.Add(handle);

          handle = handle.GetInteriorHandle();
          if (handle != null) _handles.Add(handle);
        }

        for (var i = curr; i < _handles.Count; i++)
          yield return _handles[i];
      } while (fetched > 0);

      _handleEnum = null;
    }

    internal override IEnumerable<ClrRoot> EnumerateStackReferences(ClrThread thread, bool includeDead)
    {
      if (includeDead)
        return base.EnumerateStackReferences(thread, includeDead);

      return EnumerateStackReferencesWorker(thread);
    }

    private IEnumerable<ClrRoot> EnumerateStackReferencesWorker(ClrThread thread)
    {
      ISOSStackRefEnum handleEnum = null;
      if (_sos.GetStackReferences(thread.OSThreadId, out var tmp) >= 0)
        handleEnum = tmp as ISOSStackRefEnum;

      var domain = GetAppDomainByAddress(thread.AppDomain);
      if (handleEnum != null)
      {
        var heap = Heap;
        var refs = new StackRefData[1024];

        const int GCInteriorFlag = 1;
        const int GCPinnedFlag = 2;
        uint fetched = 0;
        do
        {
          if (handleEnum.Next((uint)refs.Length, refs, out fetched) < 0)
            break;

          for (uint i = 0; i < fetched && i < refs.Length; ++i)
          {
            if (refs[i].Object == 0)
              continue;

            var pinned = (refs[i].Flags & GCPinnedFlag) == GCPinnedFlag;
            var interior = (refs[i].Flags & GCInteriorFlag) == GCInteriorFlag;

            ClrType type = null;

            if (!interior)
              type = heap.GetObjectType(refs[i].Object);

            var frame = thread.StackTrace.SingleOrDefault(
              f => f.StackPointer == refs[i].Source || f.StackPointer == refs[i].StackPointer && f.InstructionPointer == refs[i].Source);

            if (interior || type != null)
              yield return new LocalVarRoot(refs[i].Address, refs[i].Object, type, domain, thread, pinned, false, interior, frame);
          }
        } while (fetched == refs.Length);
      }
    }

    internal override ulong GetFirstThread()
    {
      var threadStore = GetThreadStoreData();
      return threadStore != null ? threadStore.FirstThread : 0;
    }

    internal override IThreadData GetThread(ulong addr)
    {
      if (addr == 0)
        return null;

      if (_sos.GetThreadData(addr, out var data) < 0)
        return null;

      return data;
    }

    internal override IHeapDetails GetSvrHeapDetails(ulong addr)
    {
      if (_sos.GetGCHeapDetails(addr, out var data) < 0)
        return null;

      return data;
    }

    internal override IHeapDetails GetWksHeapDetails()
    {
      if (_sos.GetGCHeapStaticData(out var data) < 0)
        return null;

      return data;
    }

    internal override ulong[] GetServerHeapList()
    {
      var refs = new ulong[HeapCount];
      if (_sos.GetGCHeapList((uint)HeapCount, refs, out var needed) < 0)
        return null;

      return refs;
    }

    internal override IList<ulong> GetAppDomainList(int count)
    {
      var data = new ulong[1024];
      if (_sos.GetAppDomainList((uint)data.Length, data, out var needed) < 0)
        return null;

      var list = new List<ulong>((int)needed);

      for (uint i = 0; i < needed; ++i)
        list.Add(data[i]);

      return list;
    }

    internal override ulong[] GetAssemblyList(ulong appDomain, int count)
    {
      int needed;
      if (count <= 0)
      {
        if (_sos.GetAssemblyList(appDomain, 0, null, out needed) < 0)
          return new ulong[0];

        count = needed;
      }

      // We ignore the return value here since modules might be partially
      // filled even if GetAssemblyList hits an error.
      var modules = new ulong[count];
      _sos.GetAssemblyList(appDomain, modules.Length, modules, out needed);

      return modules;
    }

    internal override ulong[] GetModuleList(ulong assembly, int count)
    {
      var needed = (uint)count;

      if (count <= 0)
        if (_sos.GetAssemblyModuleList(assembly, 0, null, out needed) < 0)
          return new ulong[0];

      // We ignore the return value here since modules might be partially
      // filled even if GetAssemblyList hits an error.
      var modules = new ulong[needed];
      _sos.GetAssemblyModuleList(assembly, needed, modules, out needed);
      return modules;
    }

    internal override IAssemblyData GetAssemblyData(ulong domain, ulong assembly)
    {
      if (_sos.GetAssemblyData(domain, assembly, out var data) < 0)
        if (data.Address != assembly)
          return null;

      return data;
    }

    internal override IAppDomainStoreData GetAppDomainStoreData()
    {
      if (_sos.GetAppDomainStoreData(out var data) < 0)
        return null;

      return data;
    }

    internal override IMethodTableData GetMethodTableData(ulong addr)
    {
      if (_sos.GetMethodTableData(addr, out var data) < 0)
        return null;

      return data;
    }

    internal override ulong GetMethodTableByEEClass(ulong eeclass)
    {
      if (_sos.GetMethodTableForEEClass(eeclass, out var value) != 0)
        return 0;

      return value;
    }

    internal override IGCInfo GetGCInfoImpl()
    {
      return _sos.GetGCHeapData(out var gcInfo) >= 0 ? (IGCInfo)gcInfo : null;
    }

    internal override bool GetCommonMethodTables(ref CommonMethodTables mCommonMTs)
    {
      return _sos.GetUsefulGlobals(out mCommonMTs) >= 0;
    }

    internal override string GetNameForMT(ulong mt)
    {
      if (_sos.GetMethodTableName(mt, 0, null, out var count) < 0)
        return null;

      var sb = new StringBuilder((int)count);
      if (_sos.GetMethodTableName(mt, count, sb, out count) < 0)
        return null;

      return sb.ToString();
    }

    internal override string GetPEFileName(ulong addr)
    {
      if (_sos.GetPEFileName(addr, 0, null, out var needed) < 0)
        return null;

      var sb = new StringBuilder((int)needed);
      if (_sos.GetPEFileName(addr, needed, sb, out needed) < 0)
        return null;

      return sb.ToString();
    }

    internal override IModuleData GetModuleData(ulong addr)
    {
      return _sos.GetModuleData(addr, out var data) >= 0 ? (IModuleData)data : null;
    }

    internal override ulong GetModuleForMT(ulong addr)
    {
      if (_sos.GetMethodTableData(addr, out var data) < 0)
        return 0;

      return data.module;
    }

    internal override ISegmentData GetSegmentData(ulong addr)
    {
      if (_sos.GetHeapSegmentData(addr, out var seg) < 0)
        return null;

      return seg;
    }

    internal override IAppDomainData GetAppDomainData(ulong addr)
    {
      var data = new LegacyAppDomainData();
      ;
      if (_sos.GetAppDomainData(addr, out data) < 0)
        if (data.Address != addr && data.StubHeap != 0)
          return null;

      return data;
    }

    internal override string GetAppDomaminName(ulong addr)
    {
      if (_sos.GetAppDomainName(addr, 0, null, out var count) < 0)
        return null;

      var sb = new StringBuilder((int)count);

      if (_sos.GetAppDomainName(addr, count, sb, out count) < 0)
        return null;

      return sb.ToString();
    }

    internal override string GetAssemblyName(ulong addr)
    {
      if (_sos.GetAssemblyName(addr, 0, null, out var count) < 0)
        return null;

      var sb = new StringBuilder((int)count);

      if (_sos.GetAssemblyName(addr, count, sb, out count) < 0)
        return null;

      return sb.ToString();
    }

    internal override bool TraverseHeap(ulong heap, LoaderHeapTraverse callback)
    {
      var res = _sos.TraverseLoaderHeap(heap, Marshal.GetFunctionPointerForDelegate(callback)) >= 0;
      GC.KeepAlive(callback);
      return res;
    }

    internal override bool TraverseStubHeap(ulong appDomain, int type, LoaderHeapTraverse callback)
    {
      var res = _sos.TraverseVirtCallStubHeap(appDomain, (uint)type, Marshal.GetFunctionPointerForDelegate(callback)) >= 0;
      GC.KeepAlive(callback);
      return res;
    }

    internal override IEnumerable<ICodeHeap> EnumerateJitHeaps()
    {
      LegacyJitManagerInfo[] jitManagers = null;

      var res = _sos.GetJitManagerList(0, null, out var needed);
      if (res >= 0)
      {
        jitManagers = new LegacyJitManagerInfo[needed];
        res = _sos.GetJitManagerList(needed, jitManagers, out needed);
      }

      if (res >= 0 && jitManagers != null)
        for (var i = 0; i < jitManagers.Length; ++i)
        {
          if (jitManagers[i].type != CodeHeapType.Unknown)
            continue;

          res = _sos.GetCodeHeapList(jitManagers[i].addr, 0, null, out needed);
          if (res >= 0 && needed > 0)
          {
            var heapInfo = new LegacyJitCodeHeapInfo[needed];
            res = _sos.GetCodeHeapList(jitManagers[i].addr, needed, heapInfo, out needed);

            if (res >= 0)
              for (var j = 0; j < heapInfo.Length; ++j)
                yield return heapInfo[i];
          }
        }
    }

    internal override IFieldInfo GetFieldInfo(ulong mt)
    {
      if (_sos.GetMethodTableFieldData(mt, out var fieldInfo) < 0)
        return null;

      return fieldInfo;
    }

    internal override IFieldData GetFieldData(ulong fieldDesc)
    {
      if (_sos.GetFieldDescData(fieldDesc, out var data) < 0)
        return null;

      return data;
    }

    internal override IMetadataImport GetMetadataImport(ulong module)
    {
      if (module == 0 || _sos.GetModule(module, out var obj) < 0)
        return null;

      RegisterForRelease(obj);
      return obj as IMetadataImport;
    }

    internal override IObjectData GetObjectData(ulong objRef)
    {
      if (_sos.GetObjectData(objRef, out var data) < 0)
        return null;

      return data;
    }

    internal override IList<MethodTableTokenPair> GetMethodTableList(ulong module)
    {
      var mts = new List<MethodTableTokenPair>();
      var res = _sos.TraverseModuleMap(
        0,
        module,
        delegate(uint index, ulong mt, IntPtr token) { mts.Add(new MethodTableTokenPair(mt, index)); },
        IntPtr.Zero);

      return mts;
    }

    internal override IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong id)
    {
      var res = _sos.GetDomainLocalModuleDataFromAppDomain(appDomain, (int)id, out var data);
      if (res < 0)
        return null;

      return data;
    }

    internal override COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count)
    {
      var data = new COMInterfacePointerData[count];
      if (_sos.GetCCWInterfaces(ccw, (uint)count, data, out var pNeeded) >= 0)
        return data;

      return null;
    }

    internal override COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count)
    {
      var data = new COMInterfacePointerData[count];
      if (_sos.GetRCWInterfaces(rcw, (uint)count, data, out var pNeeded) >= 0)
        return data;

      return null;
    }

    internal override ICCWData GetCCWData(ulong ccw)
    {
      if (ccw != 0 && _sos.GetCCWData(ccw, out var data) >= 0)
        return data;

      return null;
    }

    internal override IRCWData GetRCWData(ulong rcw)
    {
      if (rcw != 0 && _sos.GetRCWData(rcw, out var data) >= 0)
        return data;

      return null;
    }

    internal override ulong GetILForModule(ClrModule module, uint rva)
    {
      return _sos.GetILForModule(module.Address, rva, out var ilAddr) == 0 ? ilAddr : 0;
    }

    internal override ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared)
    {
      ulong addr = offset;

      if (_sos.GetThreadLocalModuleData(thread, moduleId, out var data) < 0)
        return 0;

      if (type.IsObjectReference() || type.IsValueClass())
        addr += data.pGCStaticDataStart;
      else
        addr += data.pNonGCStaticDataStart;

      return addr;
    }

    internal override IDomainLocalModuleData GetDomainLocalModule(ulong module)
    {
      if (_sos.GetDomainLocalModuleDataFromModule(module, out var data) < 0)
        return null;

      return data;
    }

    internal override IList<ulong> GetMethodDescList(ulong methodTable)
    {
      if (_sos.GetMethodTableData(methodTable, out var mtData) < 0)
        return null;

      var mds = new List<ulong>(mtData.wNumMethods);

      ulong ip = 0;
      for (uint i = 0; i < mtData.wNumMethods; ++i)
        if (_sos.GetMethodTableSlot(methodTable, i, out ip) >= 0)
          if (_sos.GetCodeHeaderData(ip, out var header) >= 0)
            mds.Add(header.MethodDescPtr);

      return mds;
    }

    internal override string GetNameForMD(ulong md)
    {
      if (_sos.GetMethodDescName(md, 0, null, out var needed) < 0)
        return "UNKNOWN";

      var sb = new StringBuilder((int)needed);
      if (_sos.GetMethodDescName(md, (uint)sb.Capacity, sb, out var actuallyNeeded) < 0)
        return "UNKNOWN";

      // Patch for a bug on sos side :
      //  Sometimes, when the target method has parameters with generic types
      //  the first call to GetMethodDescName sets an incorrect value into pNeeded.
      //  In those cases, a second call directly after the first returns the correct value.
      if (needed != actuallyNeeded)
      {
        sb.Capacity = (int)actuallyNeeded;
        if (_sos.GetMethodDescName(md, (uint)sb.Capacity, sb, out actuallyNeeded) < 0)
          return "UNKNOWN";
      }

      return sb.ToString();
    }

    internal override IMethodDescData GetMethodDescData(ulong md)
    {
      var wrapper = new V45MethodDescDataWrapper();
      if (!wrapper.Init(_sos, md))
        return null;

      return wrapper;
    }

    internal override uint GetMetadataToken(ulong mt)
    {
      if (_sos.GetMethodTableData(mt, out var data) < 0)
        return uint.MaxValue;

      return data.token;
    }

    protected override DesktopStackFrame GetStackFrame(DesktopThread thread, int res, ulong ip, ulong framePtr, ulong frameVtbl)
    {
      DesktopStackFrame frame;
      var sb = new StringBuilder(256);
      if (res >= 0 && frameVtbl != 0)
      {
        ClrMethod innerMethod = null;
        var frameName = "Unknown Frame";
        if (_sos.GetFrameName(frameVtbl, (uint)sb.Capacity, sb, out var needed) >= 0)
          frameName = sb.ToString();

        if (_sos.GetMethodDescPtrFromFrame(framePtr, out var md) == 0)
        {
          var mdData = new V45MethodDescDataWrapper();
          if (mdData.Init(_sos, md))
            innerMethod = DesktopMethod.Create(this, mdData);
        }

        frame = new DesktopStackFrame(this, thread, framePtr, frameName, innerMethod);
      }
      else
      {
        if (_sos.GetMethodDescPtrFromIP(ip, out var md) >= 0)
          frame = new DesktopStackFrame(this, thread, ip, framePtr, md);
        else
          frame = new DesktopStackFrame(this, thread, ip, framePtr, 0);
      }

      return frame;
    }

    private bool GetStackTraceFromField(ClrType type, ulong obj, out ulong stackTrace)
    {
      stackTrace = 0;
      var field = type.GetFieldByName("_stackTrace");
      if (field == null)
        return false;

      var tmp = field.GetValue(obj);
      if (tmp == null || !(tmp is ulong))
        return false;

      stackTrace = (ulong)tmp;
      return true;
    }

    internal override IList<ClrStackFrame> GetExceptionStackTrace(ulong obj, ClrType type)
    {
      // TODO: Review this and if it works on v4.5, merge the two implementations back into RuntimeBase.
      var result = new List<ClrStackFrame>();
      if (type == null)
        return result;

      if (!GetStackTraceFromField(type, obj, out var _stackTrace))
        if (!ReadPointer(obj + GetStackTraceOffset(), out _stackTrace))
          return result;

      if (_stackTrace == 0)
        return result;

      var heap = Heap;
      var stackTraceType = heap.GetObjectType(_stackTrace);
      if (stackTraceType == null || !stackTraceType.IsArray)
        return result;

      var len = stackTraceType.GetArrayLength(_stackTrace);
      if (len == 0)
        return result;

      var elementSize = IntPtr.Size * 4;
      var dataPtr = _stackTrace + (ulong)(IntPtr.Size * 2);
      if (!ReadPointer(dataPtr, out var count))
        return result;

      // Skip size and header
      dataPtr += (ulong)(IntPtr.Size * 2);

      DesktopThread thread = null;
      for (var i = 0; i < (int)count; ++i)
      {
        if (!ReadPointer(dataPtr, out var ip))
          break;
        if (!ReadPointer(dataPtr + (ulong)IntPtr.Size, out var sp))
          break;
        if (!ReadPointer(dataPtr + (ulong)(2 * IntPtr.Size), out var md))
          break;

        if (i == 0 && sp != 0)
          thread = (DesktopThread)GetThreadByStackAddress(sp);

        // it seems that the first frame often has 0 for IP and SP.  Try the 2nd frame as well
        if (i == 1 && thread == null && sp != 0)
          thread = (DesktopThread)GetThreadByStackAddress(sp);

        result.Add(new DesktopStackFrame(this, thread, ip, sp, md));

        dataPtr += (ulong)elementSize;
      }

      return result;
    }

    internal override IThreadStoreData GetThreadStoreData()
    {
      if (_sos.GetThreadStoreData(out var data) < 0)
        return null;

      return data;
    }

    internal override string GetAppBase(ulong appDomain)
    {
      if (_sos.GetApplicationBase(appDomain, 0, null, out var needed) < 0)
        return null;

      var builder = new StringBuilder((int)needed);
      if (_sos.GetApplicationBase(appDomain, (int)needed, builder, out needed) < 0)
        return null;

      return builder.ToString();
    }

    internal override string GetConfigFile(ulong appDomain)
    {
      if (_sos.GetAppDomainConfigFile(appDomain, 0, null, out var needed) < 0)
        return null;

      var builder = new StringBuilder((int)needed);
      if (_sos.GetAppDomainConfigFile(appDomain, (int)needed, builder, out needed) < 0)
        return null;

      return builder.ToString();
    }

    internal override IMethodDescData GetMDForIP(ulong ip)
    {
      if (_sos.GetMethodDescPtrFromIP(ip, out var md) < 0 || md == 0)
      {
        if (_sos.GetCodeHeaderData(ip, out var codeHeaderData) < 0)
          return null;

        if ((md = codeHeaderData.MethodDescPtr) == 0)
          return null;
      }

      var mdWrapper = new V45MethodDescDataWrapper();
      if (!mdWrapper.Init(_sos, md))
        return null;

      return mdWrapper;
    }

    protected override ulong GetThreadFromThinlock(uint threadId)
    {
      if (_sos.GetThreadFromThinlockID(threadId, out var thread) < 0)
        return 0;

      return thread;
    }

    internal override int GetSyncblkCount()
    {
      if (_sos.GetSyncBlockData(1, out var data) < 0)
        return 0;

      return (int)data.TotalCount;
    }

    internal override ISyncBlkData GetSyncblkData(int index)
    {
      if (_sos.GetSyncBlockData((uint)index + 1, out var data) < 0)
        return null;

      return data;
    }

    internal override IThreadPoolData GetThreadPoolData()
    {
      if (_sos.GetThreadpoolData(out var data) < 0)
        return null;

      return data;
    }

    internal override uint GetTlsSlot()
    {
      if (_sos.GetTLSIndex(out var result) < 0)
        return uint.MaxValue;

      return result;
    }

    internal override uint GetThreadTypeIndex()
    {
      return 11;
    }

    protected override uint GetRWLockDataOffset()
    {
      if (PointerSize == 8)
        return 0x30;

      return 0x18;
    }

    internal override IEnumerable<NativeWorkItem> EnumerateWorkItems()
    {
      if (_sos.GetThreadpoolData(out var data) == 0)
      {
        var request = data.FirstWorkRequest;
        while (request != 0)
        {
          if (_sos.GetWorkRequestData(request, out var requestData) != 0)
            break;

          yield return new DesktopNativeWorkItem(requestData);

          request = requestData.NextWorkRequest;
        }
      }
    }

    internal override uint GetStringFirstCharOffset()
    {
      if (PointerSize == 8)
        return 0xc;

      return 8;
    }

    internal override uint GetStringLengthOffset()
    {
      if (PointerSize == 8)
        return 0x8;

      return 0x4;
    }

    internal override uint GetExceptionHROffset()
    {
      return PointerSize == 8 ? 0x8cu : 0x40u;
    }
  }
}