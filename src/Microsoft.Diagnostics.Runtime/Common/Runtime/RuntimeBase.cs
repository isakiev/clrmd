﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime
{
  internal abstract class RuntimeBase : ClrRuntime
  {
    private static readonly ulong[] s_emptyPointerArray = new ulong[0];
    protected DacLibrary _library;
    protected IXCLRDataProcess _dacInterface;
    private MemoryReader _cache;
    protected IDataReader _dataReader;
    protected DataTargetImpl _dataTarget;

    protected ICorDebugProcess _corDebugProcess;
    internal ICorDebugProcess CorDebugProcess
    {
      get
      {
        if (_corDebugProcess == null)
          _corDebugProcess = CLRDebugging.CreateICorDebugProcess(ClrInfo.ModuleInfo.ImageBase, _library.DacDataTarget, _dataTarget.FileLoader);

        return _corDebugProcess;
      }
    }

    public RuntimeBase(ClrInfo info, DataTargetImpl dataTarget, DacLibrary lib)
    {
      Debug.Assert(lib != null);
      Debug.Assert(lib.DacInterface != null);

      ClrInfo = info;
      _dataTarget = dataTarget;
      _library = lib;
      _dacInterface = _library.DacInterface;
      InitApi();

      _dacInterface.Flush();

      var data = GetGCInfo();
      if (data != null)
      {
        ServerGC = data.ServerMode;
        HeapCount = data.HeapCount;
        CanWalkHeap = data.GCStructuresValid;
      }

      _dataReader = dataTarget.DataReader;
    }

    public override DataTarget DataTarget => _dataTarget;

    public void RegisterForRelease(object o)
    {
      if (o != null)
        _library.AddToReleaseList(o);
    }

    public void RegisterForRelease(IModuleData module)
    {
      RegisterForRelease(module?.LegacyMetaDataImport);
    }

    public IDataReader DataReader => _dataReader;

    protected abstract void InitApi();

    public override int PointerSize => IntPtr.Size;

    protected internal bool CanWalkHeap { get; protected set; }

    internal MemoryReader MemoryReader
    {
      get
      {
        if (_cache == null)
          _cache = new MemoryReader(DataReader, 0x200);
        return _cache;
      }
      set => _cache = value;
    }

    internal bool GetHeaps(out SubHeap[] heaps)
    {
      heaps = new SubHeap[HeapCount];
      var allocContexts = GetAllocContexts();
      if (ServerGC)
      {
        var heapList = GetServerHeapList();
        if (heapList == null)
          return false;

        var succeeded = false;
        for (var i = 0; i < heapList.Length; ++i)
        {
          var heap = GetSvrHeapDetails(heapList[i]);
          if (heap == null)
            continue;

          heaps[i] = new SubHeap(heap, i, allocContexts);
          if (heap.EphemeralAllocContextPtr != 0)
            heaps[i].AllocPointers[heap.EphemeralAllocContextPtr] = heap.EphemeralAllocContextLimit;

          succeeded = true;
        }

        return succeeded;
      }

      {
        var heap = GetWksHeapDetails();
        if (heap == null)
          return false;

        heaps[0] = new SubHeap(heap, 0, allocContexts);
        heaps[0].AllocPointers[heap.EphemeralAllocContextPtr] = heap.EphemeralAllocContextLimit;

        return true;
      }
    }

    internal Dictionary<ulong, ulong> GetAllocContexts()
    {
      var ret = new Dictionary<ulong, ulong>();

      // Give a max number of threads to walk to ensure no infinite loops due to data
      // inconsistency.
      var max = 1024;

      var thread = GetThread(GetFirstThread());

      while (max-- > 0 && thread != null)
      {
        if (thread.AllocPtr != 0)
          ret[thread.AllocPtr] = thread.AllocLimit;

        if (thread.Next == 0)
          break;

        thread = GetThread(thread.Next);
      }

      return ret;
    }

    private struct StackRef
    {
      public ulong Address;
      public ulong Object;

      public StackRef(ulong stackPtr, ulong objRef)
      {
        Address = stackPtr;
        Object = objRef;
      }
    }

    public override IEnumerable<ulong> EnumerateFinalizerQueueObjectAddresses()
    {
      if (GetHeaps(out var heaps))
        foreach (var heap in heaps)
        {
          foreach (var objAddr in GetPointersInRange(heap.FQStart, heap.FQStop))
            if (objAddr != 0)
              yield return objAddr;
        }
    }

    internal virtual IEnumerable<ClrRoot> EnumerateStackReferences(ClrThread thread, bool includeDead)
    {
      var stackBase = thread.StackBase;
      var stackLimit = thread.StackLimit;
      if (stackLimit <= stackBase)
      {
        var tmp = stackLimit;
        stackLimit = stackBase;
        stackBase = tmp;
      }

      var domain = GetAppDomainByAddress(thread.AppDomain);
      var heap = Heap;
      var mask = (ulong)(PointerSize - 1);
      var cache = MemoryReader;
      cache.EnsureRangeInCache(stackBase);
      for (var stackPtr = stackBase; stackPtr < stackLimit; stackPtr += (uint)PointerSize)
        if (cache.ReadPtr(stackPtr, out var objRef))
          if (heap.IsInHeap(objRef))
            if (heap.ReadPointer(objRef, out var mt))
            {
              ClrType type = null;

              if (mt > 1024)
                type = heap.GetObjectType(objRef);

              if (type != null && !type.IsFree)
                yield return new LocalVarRoot(stackPtr, objRef, type, domain, thread, false, true, false, null);
            }
    }

    internal abstract ulong GetFirstThread();
    internal abstract IThreadData GetThread(ulong addr);
    internal abstract IHeapDetails GetSvrHeapDetails(ulong addr);
    internal abstract IHeapDetails GetWksHeapDetails();
    internal abstract ulong[] GetServerHeapList();
    internal abstract IThreadStoreData GetThreadStoreData();
    internal abstract ISegmentData GetSegmentData(ulong addr);
    internal abstract IGCInfo GetGCInfo();
    internal abstract IMethodTableData GetMethodTableData(ulong addr);
    internal abstract uint GetTlsSlot();
    internal abstract uint GetThreadTypeIndex();

    internal abstract ClrAppDomain GetAppDomainByAddress(ulong addr);

    protected bool Request(uint id, ulong param, byte[] output)
    {
      var input = BitConverter.GetBytes(param);

      return Request(id, input, output);
    }

    protected bool Request(uint id, uint param, byte[] output)
    {
      var input = BitConverter.GetBytes(param);

      return Request(id, input, output);
    }

    protected bool Request(uint id, byte[] input, byte[] output)
    {
      uint inSize = 0;
      if (input != null)
        inSize = (uint)input.Length;

      uint outSize = 0;
      if (output != null)
        outSize = (uint)output.Length;

      var result = _dacInterface.Request(id, inSize, input, outSize, output);

      return result >= 0;
    }

    protected I Request<I, T>(uint id, byte[] input)
      where T : struct, I
      where I : class
    {
      var output = GetByteArrayForStruct<T>();

      if (!Request(id, input, output))
        return null;

      return ConvertStruct<I, T>(output);
    }

    protected I Request<I, T>(uint id, ulong param)
      where T : struct, I
      where I : class
    {
      var output = GetByteArrayForStruct<T>();

      if (!Request(id, param, output))
        return null;

      return ConvertStruct<I, T>(output);
    }

    protected I Request<I, T>(uint id, uint param)
      where T : struct, I
      where I : class
    {
      var output = GetByteArrayForStruct<T>();

      if (!Request(id, param, output))
        return null;

      return ConvertStruct<I, T>(output);
    }

    protected I Request<I, T>(uint id)
      where T : struct, I
      where I : class
    {
      var output = GetByteArrayForStruct<T>();

      if (!Request(id, null, output))
        return null;

      return ConvertStruct<I, T>(output);
    }

    protected bool RequestStruct<T>(uint id, ref T t)
      where T : struct
    {
      var output = GetByteArrayForStruct<T>();

      if (!Request(id, null, output))
        return false;

      var handle = GCHandle.Alloc(output, GCHandleType.Pinned);
      t = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
      handle.Free();
      return true;
    }

    protected bool RequestStruct<T>(uint id, ulong addr, ref T t)
      where T : struct
    {
      var input = new byte[sizeof(ulong)];
      var output = GetByteArrayForStruct<T>();

      WriteValueToBuffer(addr, input, 0);

      if (!Request(id, input, output))
        return false;

      var handle = GCHandle.Alloc(output, GCHandleType.Pinned);
      t = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
      handle.Free();
      return true;
    }

    protected ulong[] RequestAddrList(uint id, int length)
    {
      var bytes = new byte[length * sizeof(ulong)];
      if (!Request(id, null, bytes))
        return null;

      var result = new ulong[length];
      for (uint i = 0; i < length; ++i)
        result[i] = BitConverter.ToUInt64(bytes, (int)(i * sizeof(ulong)));

      return result;
    }

    protected ulong[] RequestAddrList(uint id, ulong param, int length)
    {
      var bytes = new byte[length * sizeof(ulong)];
      if (!Request(id, param, bytes))
        return null;

      var result = new ulong[length];
      for (uint i = 0; i < length; ++i)
        result[i] = BitConverter.ToUInt64(bytes, (int)(i * sizeof(ulong)));

      return result;
    }

    protected static string BytesToString(byte[] output)
    {
      var len = 0;
      while (len < output.Length && (output[len] != 0 || output[len + 1] != 0))
        len += 2;

      if (len > output.Length)
        len = output.Length;

      return Encoding.Unicode.GetString(output, 0, len);
    }

    protected byte[] GetByteArrayForStruct<T>()
      where T : struct
    {
      return new byte[Marshal.SizeOf(typeof(T))];
    }

    protected I ConvertStruct<I, T>(byte[] bytes)
      where I : class
      where T : I
    {
      var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      var result = (I)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
      handle.Free();
      return result;
    }

    protected int WriteValueToBuffer(IntPtr ptr, byte[] buffer, int offset)
    {
      var value = (ulong)ptr.ToInt64();
      for (var i = offset; i < offset + IntPtr.Size; ++i)
      {
        buffer[i] = (byte)value;
        value >>= 8;
      }

      return offset + IntPtr.Size;
    }

    protected int WriteValueToBuffer(int value, byte[] buffer, int offset)
    {
      for (var i = offset; i < offset + sizeof(int); ++i)
      {
        buffer[i] = (byte)value;
        value >>= 8;
      }

      return offset + sizeof(int);
    }

    protected int WriteValueToBuffer(uint value, byte[] buffer, int offset)
    {
      for (var i = offset; i < offset + sizeof(int); ++i)
      {
        buffer[i] = (byte)value;
        value >>= 8;
      }

      return offset + sizeof(int);
    }

    protected int WriteValueToBuffer(ulong value, byte[] buffer, int offset)
    {
      for (var i = offset; i < offset + sizeof(ulong); ++i)
      {
        buffer[i] = (byte)value;
        value >>= 8;
      }

      return offset + sizeof(ulong);
    }

    public override bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
    {
      return _dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
    }

    private readonly byte[] _dataBuffer = new byte[8];

    public bool ReadByte(ulong addr, out byte value)
    {
      // todo: There's probably a more efficient way to implement this if ReadVirtual accepted an "out byte"
      //       "out dword", "out long", etc.
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, 1, out var read))
        return false;

      Debug.Assert(read == 1);

      value = _dataBuffer[0];
      return true;
    }

    public bool ReadByte(ulong addr, out sbyte value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, 1, out var read))
        return false;

      Debug.Assert(read == 1);

      value = (sbyte)_dataBuffer[0];
      return true;
    }

    public bool ReadDword(ulong addr, out int value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, sizeof(int), out var read))
        return false;

      Debug.Assert(read == 4);

      value = BitConverter.ToInt32(_dataBuffer, 0);
      return true;
    }

    public bool ReadDword(ulong addr, out uint value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, sizeof(uint), out var read))
        return false;

      Debug.Assert(read == 4);

      value = BitConverter.ToUInt32(_dataBuffer, 0);
      return true;
    }

    public bool ReadFloat(ulong addr, out float value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, sizeof(float), out var read))
        return false;

      Debug.Assert(read == sizeof(float));

      value = BitConverter.ToSingle(_dataBuffer, 0);
      return true;
    }

    public bool ReadFloat(ulong addr, out double value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, sizeof(double), out var read))
        return false;

      Debug.Assert(read == sizeof(double));

      value = BitConverter.ToDouble(_dataBuffer, 0);
      return true;
    }

    public bool ReadString(ulong addr, out string value)
    {
      value = ((DesktopGCHeap)Heap).GetStringContents(addr);
      return value != null;
    }

    public bool ReadShort(ulong addr, out short value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, sizeof(short), out var read))
        return false;

      Debug.Assert(read == sizeof(short));

      value = BitConverter.ToInt16(_dataBuffer, 0);
      return true;
    }

    public bool ReadShort(ulong addr, out ushort value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, sizeof(ushort), out var read))
        return false;

      Debug.Assert(read == sizeof(ushort));

      value = BitConverter.ToUInt16(_dataBuffer, 0);
      return true;
    }

    public bool ReadQword(ulong addr, out ulong value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, sizeof(ulong), out var read))
        return false;

      Debug.Assert(read == sizeof(ulong));

      value = BitConverter.ToUInt64(_dataBuffer, 0);
      return true;
    }

    public bool ReadQword(ulong addr, out long value)
    {
      value = 0;
      if (!ReadMemory(addr, _dataBuffer, sizeof(long), out var read))
        return false;

      Debug.Assert(read == sizeof(long));

      value = BitConverter.ToInt64(_dataBuffer, 0);
      return true;
    }

    public override bool ReadPointer(ulong addr, out ulong value)
    {
      var ptrSize = PointerSize;
      if (!ReadMemory(addr, _dataBuffer, ptrSize, out var read))
      {
        value = 0xcccccccc;
        return false;
      }

      Debug.Assert(read == ptrSize);

      if (ptrSize == 4)
        value = BitConverter.ToUInt32(_dataBuffer, 0);
      else
        value = BitConverter.ToUInt64(_dataBuffer, 0);

      return true;
    }

    internal IEnumerable<ulong> GetPointersInRange(ulong start, ulong stop)
    {
      // Possible we have empty list, or inconsistent data.
      if (start >= stop)
        return s_emptyPointerArray;

      // Enumerate individually if we have too many.
      var count = (stop - start) / (ulong)IntPtr.Size;
      if (count > 4096)
        return EnumeratePointersInRange(start, stop);

      var array = new ulong[count];
      var tmp = new byte[(int)count * IntPtr.Size];
      if (!ReadMemory(start, tmp, tmp.Length, out var read))
        return s_emptyPointerArray;

      if (IntPtr.Size == 4)
        for (uint i = 0; i < array.Length; ++i)
          array[i] = BitConverter.ToUInt32(tmp, (int)(i * IntPtr.Size));
      else
        for (uint i = 0; i < array.Length; ++i)
          array[i] = BitConverter.ToUInt64(tmp, (int)(i * IntPtr.Size));

      return array;
    }

    private IEnumerable<ulong> EnumeratePointersInRange(ulong start, ulong stop)
    {
      for (var ptr = start; ptr < stop; ptr += (uint)IntPtr.Size)
      {
        if (!ReadPointer(ptr, out var obj))
          break;

        yield return obj;
      }
    }
  }
}