﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
//  
//  This provides a minidump reader for managed code.
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

// This provides a managed wrapper over the unmanaged dump-reading APIs in DbgHelp.dll.
// 
// ** This has several advantages:
// - type-safe wrappers
// - marshal minidump data-structures into the proper managed types (System.String,
// System.DateTime, System.Version, System.OperatingSystem, etc)
// 
// This does not validate aganst corrupted dumps. 
// 
// ** This is not a complete set of wrappers. 
// Other potentially interesting things to expose from the dump file:
// - the header. (Get flags, Version)
// - Exception stream (find last exception thrown)
// - 
// 
// ** Potential Performance improvements
// This was first prototyped in unmanaged C++, and was significantly faster there. 
// This is  not optimized for performance at all. It currently does not use unsafe C# and so has
// no pointers to structures and so has high costs from Marshal.PtrToStructure(typeof(T)) instead of
// just using T*. 
// This could probably be speed up signficantly (approaching the speed of the C++ prototype) by using unsafe C#. 
// 
// More data could be cached. A full dump may be 80 MB+, so caching extra data for faster indexing
// and lookup, especially for reading the memory stream.
// However, the app consuming the DumpReader is probably doing a lot of caching on its own, so
// extra caching in the dump reader may make microbenchmarks go faster, but just increase the
// working set and complexity of real clients.
// 
//     

namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  /// <summary>
  ///   Read contents of a minidump.
  ///   If we have a 32-bit dump, then there's an addressing collision possible.
  ///   OS debugging code sign extends 32 bit wide addresses into 64 bit wide addresses.
  ///   The CLR does not sign extend, thus you cannot round-trip target addresses exposed by this class.
  ///   Currently we read these addresses once and don't hand them back, so it's not an issue.
  /// </summary>
  internal class DumpReader : IDisposable
  {
    // Get a DumpPointer from a MINIDUMP_LOCATION_DESCRIPTOR
    protected internal DumpPointer TranslateDescriptor(MINIDUMP_LOCATION_DESCRIPTOR location)
    {
      // A Location has both an RVA and Size. If we just TranslateRVA, then that would be a
      // DumpPointer associated with a larger size (to the end of the dump-file). 
      var p = TranslateRVA(location.Rva);
      p.Shrink(location.DataSize);
      return p;
    }

    /// <summary>
    ///   Translates from an RVA to Dump Pointer.
    /// </summary>
    /// <param name="rva">RVA within the dump</param>
    /// <returns>DumpPointer representing RVA.</returns>
    protected internal DumpPointer TranslateRVA(ulong rva)
    {
      return _base.Adjust(rva);
    }

    /// <summary>
    ///   Translates from an RVA to Dump Pointer.
    /// </summary>
    /// <param name="rva">RVA within the dump</param>
    /// <returns>DumpPointer representing RVA.</returns>
    protected internal DumpPointer TranslateRVA(RVA rva)
    {
      return _base.Adjust(rva.Value);
    }

    /// <summary>
    ///   Translates from an RVA to Dump Pointer.
    /// </summary>
    /// <param name="rva">RVA within the dump</param>
    /// <returns>DumpPointer representing RVA.</returns>
    protected internal DumpPointer TranslateRVA(RVA64 rva)
    {
      return _base.Adjust(rva.Value);
    }

    /// <summary>
    ///   Gets a MINIDUMP_STRING at the given RVA as an System.String.
    /// </summary>
    /// <param name="rva">RVA of MINIDUMP_STRING</param>
    /// <returns>System.String representing contents of MINIDUMP_STRING at the given RVA</returns>
    protected internal string GetString(RVA rva)
    {
      var p = TranslateRVA(rva);
      return GetString(p);
    }

    /// <summary>
    ///   Gets a MINIDUMP_STRING at the given DumpPointer as an System.String.
    /// </summary>
    /// <param name="ptr">DumpPointer to a MINIDUMP_STRING</param>
    /// <returns>
    ///   System.String representing contents of MINIDUMP_STRING at the given location
    ///   in the dump
    /// </returns>
    protected internal string GetString(DumpPointer ptr)
    {
      EnsureValid();

      // Minidump string is defined as:
      // typedef struct _MINIDUMP_STRING {
      //   ULONG32 Length;         // Length in bytes of the string
      //    WCHAR   Buffer [0];     // Variable size buffer
      // } MINIDUMP_STRING, *PMINIDUMP_STRING;
      var lengthBytes = ptr.ReadInt32();

      ptr = ptr.Adjust(4); // move past the Length field

      var lengthChars = lengthBytes / 2;
      var s = ptr.ReadAsUnicodeString(lengthChars);
      return s;
    }

    public bool VirtualQuery(ulong addr, out VirtualQueryData data)
    {
      uint min = 0, max = (uint)_memoryChunks.Count - 1;

      while (min <= max)
      {
        var mid = (max + min) / 2;

        var targetStartAddress = _memoryChunks.StartAddress(mid);

        if (addr < targetStartAddress)
        {
          max = mid - 1;
        }
        else
        {
          var targetEndAddress = _memoryChunks.EndAddress(mid);
          if (targetEndAddress < addr)
          {
            min = mid + 1;
          }
          else
          {
            data = new VirtualQueryData(targetStartAddress, _memoryChunks.Size(mid));
            return true;
          }
        }
      }

      data = new VirtualQueryData();
      return false;
    }

    public IEnumerable<VirtualQueryData> EnumerateMemoryRanges(ulong startAddress, ulong endAddress)
    {
      for (ulong i = 0; i < _memoryChunks.Count; i++)
      {
        var targetStartAddress = _memoryChunks.StartAddress(i);
        var targetEndAddress = _memoryChunks.EndAddress(i);

        if (targetEndAddress < startAddress)
          continue;
        if (endAddress < targetStartAddress)
          continue;

        var size = _memoryChunks.Size(i);
        yield return new VirtualQueryData(targetStartAddress, size);
      }
    }

    /// <summary>
    ///   Read memory from the dump file and return results in newly allocated buffer
    /// </summary>
    /// <param name="targetAddress">target address in dump to read length bytes from</param>
    /// <param name="length">number of bytes to read</param>
    /// <returns>newly allocated byte array containing dump memory</returns>
    /// <remarks>All memory requested must be readable or it throws.</remarks>
    public byte[] ReadMemory(ulong targetAddress, int length)
    {
      var buffer = new byte[length];
      ReadMemory(targetAddress, buffer, length);
      return buffer;
    }

    /// <summary>
    ///   Read memory from the dump file and copy into the buffer
    /// </summary>
    /// <param name="targetAddress">target address in dump to read buffer.Length bytets from</param>
    /// <param name="buffer">destination buffer to copy target memory to.</param>
    /// <param name="cbRequestSize">count of bytes to read</param>
    /// <remarks>All memory requested must be readable or it throws.</remarks>
    public void ReadMemory(ulong targetAddress, byte[] buffer, int cbRequestSize)
    {
      var h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
      try
      {
        ReadMemory(targetAddress, h.AddrOfPinnedObject(), (uint)cbRequestSize);
      }
      finally
      {
        h.Free();
      }
    }

    /// <summary>
    ///   Read memory from target and copy it to the local buffer pointed to by
    ///   destinationBuffer. Throw if any portion of the requested memory is unavailable.
    /// </summary>
    /// <param name="targetRequestStart">
    ///   target address in dump file to copy
    ///   destinationBufferSizeInBytes bytes from.
    /// </param>
    /// <param name="destinationBuffer">pointer to copy the memory to.</param>
    /// <param name="destinationBufferSizeInBytes">size of the destinationBuffer in bytes.</param>
    public void ReadMemory(ulong targetRequestStart, IntPtr destinationBuffer, uint destinationBufferSizeInBytes)
    {
      var bytesRead = ReadPartialMemory(targetRequestStart, destinationBuffer, destinationBufferSizeInBytes);
      if (bytesRead != destinationBufferSizeInBytes)
        throw new ClrDiagnosticsException(
          string.Format(
            CultureInfo.CurrentUICulture,
            "Memory missing at {0}. Could only read {1} bytes of {2} total bytes requested.",
            targetRequestStart.ToString("x"),
            bytesRead,
            destinationBufferSizeInBytes),
          ClrDiagnosticsExceptionKind.CrashDumpError);
    }

    /*

    /// <summary>
    /// Read memory from the dump file and copy into the buffer
    /// </summary>
    /// <param name="targetAddress">target address in dump to read buffer.Length bytets from</param>
    /// <param name="buffer">destination buffer to copy target memory to.</param>
    /// <remarks>All memory requested must be readable or it throws.</remarks>
    public uint ReadPartialMemory(ulong targetAddress, byte[] buffer)
    {
        GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            uint cbRequestSize = (uint)buffer.Length;
            return ReadPartialMemory(targetAddress, h.AddrOfPinnedObject(), cbRequestSize);
        }
        finally
        {
            h.Free();
        }
    }
    
     */
    /// <summary>
    ///   Read memory from target and copy it to the local buffer pointed to by destinationBuffer.
    /// </summary>
    /// <param name="targetRequestStart">
    ///   target address in dump file to copy
    ///   destinationBufferSizeInBytes bytes from.
    /// </param>
    /// <param name="destinationBuffer">pointer to copy the memory to.</param>
    /// <param name="destinationBufferSizeInBytes">size of the destinationBuffer in bytes.</param>
    /// <returns>Number of contiguous bytes successfuly copied into the destination buffer.</returns>
    public virtual uint ReadPartialMemory(ulong targetRequestStart, IntPtr destinationBuffer, uint destinationBufferSizeInBytes)
    {
      var bytesRead = ReadPartialMemoryInternal(
        targetRequestStart,
        destinationBuffer,
        destinationBufferSizeInBytes,
        0);
      return bytesRead;
    }

    internal ulong ReadPointerUnsafe(ulong addr)
    {
      var chunkIndex = _memoryChunks.GetChunkContainingAddress(addr);
      if (chunkIndex == -1)
        return 0;

      var chunk = TranslateRVA(_memoryChunks.RVA((uint)chunkIndex));
      var offset = addr - _memoryChunks.StartAddress((uint)chunkIndex);

      if (IntPtr.Size == 4)
        return chunk.Adjust(offset).GetDword();

      return chunk.Adjust(offset).GetUlong();
    }

    internal uint ReadDwordUnsafe(ulong addr)
    {
      var chunkIndex = _memoryChunks.GetChunkContainingAddress(addr);
      if (chunkIndex == -1)
        return 0;

      var chunk = TranslateRVA(_memoryChunks.RVA((uint)chunkIndex));
      var offset = addr - _memoryChunks.StartAddress((uint)chunkIndex);
      return chunk.Adjust(offset).GetDword();
    }

    public virtual int ReadPartialMemory(ulong targetRequestStart, byte[] destinationBuffer, int bytesRequested)
    {
      EnsureValid();

      if (bytesRequested <= 0)
        return 0;

      if (bytesRequested > destinationBuffer.Length)
        bytesRequested = destinationBuffer.Length;

      var bytesRead = 0;
      do
      {
        var chunkIndex = _memoryChunks.GetChunkContainingAddress(targetRequestStart + (uint)bytesRead);
        if (chunkIndex == -1)
          break;

        var pointerCurrentChunk = TranslateRVA(_memoryChunks.RVA((uint)chunkIndex));
        var startAddr = targetRequestStart + (uint)bytesRead - _memoryChunks.StartAddress((uint)chunkIndex);
        var bytesAvailable = _memoryChunks.Size((uint)chunkIndex) - startAddr;

        Debug.Assert(bytesRequested >= bytesRead);
        var bytesToCopy = bytesRequested - bytesRead;
        if (bytesAvailable < (uint)bytesToCopy)
          bytesToCopy = (int)bytesAvailable;

        Debug.Assert(bytesToCopy > 0);
        if (bytesToCopy == 0)
          break;

        pointerCurrentChunk.Adjust(startAddr).Copy(destinationBuffer, bytesRead, bytesToCopy);
        bytesRead += bytesToCopy;
      } while (bytesRead < bytesRequested);

      return bytesRead;
    }

#pragma warning disable 0420
    private volatile bool _disposing;
    private volatile int _lock;

    private bool AcquireReadLock()
    {
      var result = 0;
      var value = 0;
      do
      {
        value = _lock;
        if (_disposing || value < 0)
          return false;

        result = Interlocked.CompareExchange(ref _lock, value + 1, value);
      } while (result != value);

      return true;
    }

    private void ReleaseReadLock()
    {
      Interlocked.Decrement(ref _lock);
    }

    private bool AcquireWriteLock()
    {
      var result = 0;
      result = Interlocked.CompareExchange(ref _lock, -1, 0);
      while (result != 0)
      {
        Thread.Sleep(50);
        result = Interlocked.CompareExchange(ref _lock, -1, 0);
      }

      return true;
    }

    private void ReleaseWriteLock()
    {
      Interlocked.Increment(ref _lock);
    }

    // Since a MemoryListStream makes no guarantees that there aren't duplicate, overlapping, or wholly contained
    // memory regions, we need to handle that.  For the purposes of this code, we presume all memory regions
    // in the dump that cover a given VA have the correct (duplicate) contents.
    protected uint ReadPartialMemoryInternal(
      ulong targetRequestStart,
      IntPtr destinationBuffer,
      uint destinationBufferSizeInBytes,
      uint startIndex)
    {
      EnsureValid();

      if (destinationBufferSizeInBytes == 0)
        return 0;

      uint bytesRead = 0;
      do
      {
        var chunkIndex = _memoryChunks.GetChunkContainingAddress(targetRequestStart + bytesRead);
        if (chunkIndex == -1)
          break;

        var pointerCurrentChunk = TranslateRVA(_memoryChunks.RVA((uint)chunkIndex));
        var idxStart = (uint)(targetRequestStart + bytesRead - _memoryChunks.StartAddress((uint)chunkIndex));
        var bytesAvailable = (uint)_memoryChunks.Size((uint)chunkIndex) - idxStart;
        var bytesNeeded = destinationBufferSizeInBytes - bytesRead;
        var bytesToCopy = Math.Min(bytesAvailable, bytesNeeded);

        Debug.Assert(bytesToCopy > 0);
        if (bytesToCopy == 0)
          break;

        var dest = new IntPtr(destinationBuffer.ToInt64() + bytesRead);
        var destSize = destinationBufferSizeInBytes - bytesRead;
        pointerCurrentChunk.Adjust(idxStart).Copy(dest, destSize, bytesToCopy);
        bytesRead += bytesToCopy;
      } while (bytesRead < destinationBufferSizeInBytes);

      return bytesRead;
    }

    // Caching the chunks avoids the cost of Marshal.PtrToStructure on every single element in the memory list.
    // Empirically, this cache provides huge performance improvements for read memory.
    // This cache could be completey removed if we used unsafe C# and just had direct pointers
    // into the mapped dump file.
    protected MinidumpMemoryChunks _memoryChunks;
    // The backup lookup method for memory that's not in the dump is to try and load the memory
    // from the same file on disk.
    protected LoadedFileMemoryLookups _mappedFileMemory;

    /// <summary>
    ///   ToString override.
    /// </summary>
    /// <returns>string description of the DumpReader.</returns>
    public override string ToString()
    {
      if (_file == null) return "Empty";

      return _file.Name;
    }

    public bool IsMinidump { get; set; }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="path">filename to open dump file</param>
    public DumpReader(string path)
    {
      _file = File.OpenRead(path);
      var length = _file.Length;

      // The dump file may be many megabytes large, so we don't want to
      // read it all at once. Instead, doing a mapping.
      _fileMapping = NativeMethods.CreateFileMapping(_file.SafeFileHandle, IntPtr.Zero, NativeMethods.PageProtection.Readonly, 0, 0, null);

      if (_fileMapping.IsInvalid)
      {
        var error = Marshal.GetHRForLastWin32Error();
        Marshal.ThrowExceptionForHR(error, new IntPtr(-1));
      }

      _view = NativeMethods.MapViewOfFile(_fileMapping, NativeMethods.FILE_MAP_READ, 0, 0, IntPtr.Zero);
      if (_view.IsInvalid)
      {
        var error = Marshal.GetHRForLastWin32Error();
        Marshal.ThrowExceptionForHR(error, new IntPtr(-1));
      }

      _base = DumpPointer.DangerousMakeDumpPointer(_view.BaseAddress, (uint)length);

      //
      // Cache stuff
      //

      DumpPointer pStream;

      // System info.            
      pStream = GetStream(MINIDUMP_STREAM_TYPE.SystemInfoStream);
      _info = pStream.PtrToStructure<MINIDUMP_SYSTEM_INFO>();

      // Memory64ListStream is present in MinidumpWithFullMemory.
      if (TryGetStream(MINIDUMP_STREAM_TYPE.Memory64ListStream, out pStream))
      {
        _memoryChunks = new MinidumpMemoryChunks(pStream, MINIDUMP_STREAM_TYPE.Memory64ListStream);
      }
      else
      {
        // MiniDumpNormal doesn't have a Memory64ListStream, it has a MemoryListStream.
        pStream = GetStream(MINIDUMP_STREAM_TYPE.MemoryListStream);
        _memoryChunks = new MinidumpMemoryChunks(pStream, MINIDUMP_STREAM_TYPE.MemoryListStream);
      }

      _mappedFileMemory = new LoadedFileMemoryLookups();
      IsMinidump = DumpNative.IsMiniDump(_view.BaseAddress);
    }

    /// <summary>
    ///   Dispose method.
    /// </summary>
    public void Dispose()
    {
      // Clear any cached objects.
      _disposing = true;
      AcquireWriteLock();

      _info = null;
      _memoryChunks = null;
      _mappedFileMemory = null;

      // All resources are backed by safe-handles, so we don't need a finalizer.
      if (_fileMapping != null)
        _fileMapping.Close();

      if (_view != null)
        _view.Close();

      if (_file != null)
        _file.Dispose();
    }

    // Helper to ensure the object is not yet disposed.
    private void EnsureValid()
    {
      if (_file == null) throw new ObjectDisposedException("DumpReader");
    }

    private readonly FileStream _file;
    private readonly SafeWin32Handle _fileMapping;
    private readonly SafeMapViewHandle _view;

    // DumpPointer (raw pointer that's aware of remaining buffer size) for start of minidump. 
    // This is useful for computing RVAs.
    private DumpPointer _base;

    // Cached info
    private MINIDUMP_SYSTEM_INFO _info;

    /// <summary>
    ///   Get a DumpPointer for the given stream. That can then be used to further decode the stream.
    /// </summary>
    /// <param name="type">type of stream to lookup</param>
    /// <returns>DumpPointer refering into the stream. </returns>
    private DumpPointer GetStream(MINIDUMP_STREAM_TYPE type)
    {
      if (!TryGetStream(type, out var stream))
        throw new ClrDiagnosticsException("Dump does not contain a " + type + " stream.", ClrDiagnosticsExceptionKind.CrashDumpError);

      return stream;
    }

    /// <summary>
    ///   Get a DumpPointer for the given stream. That can then be used to further decode the stream.
    /// </summary>
    /// <param name="type">type of stream to lookup</param>
    /// <param name="stream">DumpPointer refering into the stream. </param>
    /// <returns>True if stream was succesfully retrived</returns>
    private bool TryGetStream(MINIDUMP_STREAM_TYPE type, out DumpPointer stream)
    {
      EnsureValid();

      var fOk = DumpNative.MiniDumpReadDumpStream(_view.BaseAddress, type, out var pStream, out var cbStreamSize);

      if (!fOk || IntPtr.Zero == pStream || cbStreamSize < 1)
      {
        stream = default(DumpPointer);
        return false;
      }

      stream = DumpPointer.DangerousMakeDumpPointer(pStream, cbStreamSize);
      return true;
    }

    /// <summary>
    ///   Version numbers of OS that this dump was taken on.
    /// </summary>
    public Version Version => _info.Version;

    /// <summary>
    ///   The processor architecture that this dump was taken on.
    /// </summary>
    public ProcessorArchitecture ProcessorArchitecture
    {
      get
      {
        EnsureValid();
        return _info.ProcessorArchitecture;
      }
    }

    /// <summary>
    ///   Get the thread for the given thread Id.
    /// </summary>
    /// <param name="threadId">thread Id to lookup.</param>
    /// <returns>
    ///   a DumpThread object representing a thread in the dump whose thread id matches
    ///   the requested id.
    /// </returns>
    public DumpThread GetThread(int threadId)
    {
      EnsureValid();
      var raw = GetRawThread(threadId);
      if (raw == null)
        return null;

      return new DumpThread(this, raw);
    }

    // Helper to get the thread list in the dump.
    private IMinidumpThreadList GetThreadList()
    {
      EnsureValid();

      DumpPointer pStream;

      MINIDUMP_STREAM_TYPE streamType;
      IMinidumpThreadList list;
      try
      {
        // On x86 and X64, we have the ThreadListStream.  On IA64, we have the ThreadExListStream.
        streamType = MINIDUMP_STREAM_TYPE.ThreadListStream;
        pStream = GetStream(streamType);
        list = new MINIDUMP_THREAD_LIST<MINIDUMP_THREAD>(pStream, streamType);
      }
      catch (ClrDiagnosticsException)
      {
        streamType = MINIDUMP_STREAM_TYPE.ThreadExListStream;
        pStream = GetStream(streamType);
        list = new MINIDUMP_THREAD_LIST<MINIDUMP_THREAD_EX>(pStream, streamType);
      }

      return list;
    }

    /// <summary>
    ///   Enumerate all the native threads in the dump
    /// </summary>
    /// <returns>an enumerate of DumpThread objects</returns>
    public IEnumerable<DumpThread> EnumerateThreads()
    {
      var list = GetThreadList();
      var num = list.Count();

      for (uint i = 0; i < num; i++)
      {
        var rawThread = list.GetElement(i);
        yield return new DumpThread(this, rawThread);
      }
    }

    // Internal helper to get the raw Minidump thread object.
    // Throws if thread is not found.
    private MINIDUMP_THREAD GetRawThread(int threadId)
    {
      var list = GetThreadList();
      var num = list.Count();

      for (uint i = 0; i < num; i++)
      {
        var thread = list.GetElement(i);
        if (threadId == thread.ThreadId) return thread;
      }

      return null;
    }

    /*
    /// <summary>
    /// Retrieve a thread context at the given location
    /// </summary>
    /// <param name="threadId">OS thread ID of the thread</param>
    /// <returns>a native context object representing the thread context</returns>
    internal NativeContext GetThreadContext(DumpReader.NativeMethods.MINIDUMP_LOCATION_DESCRIPTOR loc)
    {
        NativeContext context = ContextAllocator.GenerateContext();
        GetThreadContext(loc, context);
        return context;
    }

    /// <summary>
    /// Retrieve a thread context at the given location
    /// </summary>
    /// <param name="threadId">OS thread ID of the thread</param>
    /// <returns>a native context object representing the thread context</returns>
    internal void GetThreadContext(DumpReader.NativeMethods.MINIDUMP_LOCATION_DESCRIPTOR loc, NativeContext context)
    {
        using (IContextDirectAccessor w = context.OpenForDirectAccess())
        {
            GetThreadContext(loc, w.RawBuffer, w.Size);
        }
    }
    */
    internal void GetThreadContext(MINIDUMP_LOCATION_DESCRIPTOR loc, IntPtr buffer, int sizeBufferBytes)
    {
      if (loc.IsNull) throw new ClrDiagnosticsException("Context not present", ClrDiagnosticsExceptionKind.CrashDumpError);

      var pContext = TranslateDescriptor(loc);
      var sizeContext = (int)loc.DataSize;

      if (sizeBufferBytes < sizeContext)
        throw new ClrDiagnosticsException(
          "Context size mismatch. Expected = 0x" + sizeBufferBytes.ToString("x") + ", Size in dump = 0x" + sizeContext.ToString("x"),
          ClrDiagnosticsExceptionKind.CrashDumpError);

      // Now copy from dump into buffer. 
      pContext.Copy(buffer, (uint)sizeContext);
    }

    // Internal helper to get the list of modules
    private MINIDUMP_MODULE_LIST GetModuleList()
    {
      EnsureValid();
      var pStream = GetStream(MINIDUMP_STREAM_TYPE.ModuleListStream);
      var list = new MINIDUMP_MODULE_LIST(pStream);

      return list;
    }

    private MINIDUMP_EXCEPTION_STREAM GetExceptionStream()
    {
      var pStream = GetStream(MINIDUMP_STREAM_TYPE.ExceptionStream);
      return new MINIDUMP_EXCEPTION_STREAM(pStream);
    }

    /// <summary>
    ///   Check on whether there's an exception stream in the dump
    /// </summary>
    /// <returns> true iff there is a MINIDUMP_EXCEPTION_STREAM in the dump. </returns>
    public bool IsExceptionStream()
    {
      var ret = true;
      try
      {
        GetExceptionStream();
      }
      catch (ClrDiagnosticsException)
      {
        ret = false;
      }

      return ret;
    }

    /// <summary>
    ///   Return the TID from the exception stream.
    /// </summary>
    /// <returns> The TID from the exception stream. </returns>
    public uint ExceptionStreamThreadId()
    {
      var es = GetExceptionStream();
      return es.ThreadId;
    }

    //todo
    /*
    public NativeContext ExceptionStreamThreadContext()
    {
        NativeMethods.MINIDUMP_EXCEPTION_STREAM es = GetExceptionStream();
        return GetThreadContext(es.ThreadContext);
    }
     */

    /// <summary>
    ///   Lookup the first module in the target with a matching.
    /// </summary>
    /// <param name="nameModule">The name can either be a matching full name, or just shortname</param>
    /// <returns>The first DumpModule that has a matching name. </returns>
    public DumpModule LookupModule(string nameModule)
    {
      var list = GetModuleList();
      var num = list.Count;

      for (uint i = 0; i < num; i++)
      {
        var module = list.GetElement(i);
        var rva = module.ModuleNameRva;

        var ptr = TranslateRVA(rva);

        var name = GetString(ptr);
        if (nameModule == name ||
          name.EndsWith(nameModule))
          return new DumpModule(this, module);
      }

      return null;
    }

    /// <summary>
    ///   Return the module containing the target address, or null if no match.
    /// </summary>
    /// <param name="targetAddress">address in target</param>
    /// <returns>
    ///   Null if no match. Else a DumpModule such that the target address is in between the range specified
    ///   by the DumpModule's .BaseAddress and .Size property
    /// </returns>
    /// <remarks>
    ///   This can be useful for symbol lookups or for using module images to
    ///   supplement memory read requests for minidumps.
    /// </remarks>
    public DumpModule TryLookupModuleByAddress(ulong targetAddress)
    {
      // This is an optimized lookup path, which avoids using IEnumerable or creating
      // unnecessary DumpModule objects.
      var list = GetModuleList();

      var num = list.Count;

      for (uint i = 0; i < num; i++)
      {
        var module = list.GetElement(i);
        var targetStart = module.BaseOfImage;
        var targetEnd = targetStart + module.SizeOfImage;
        if (targetStart <= targetAddress && targetEnd > targetAddress) return new DumpModule(this, module);
      }

      return null;
    }

    /// <summary>
    ///   Enumerate all the modules in the dump.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<DumpModule> EnumerateModules()
    {
      var list = GetModuleList();

      var num = list.Count;

      for (uint i = 0; i < num; i++)
      {
        var module = list.GetElement(i);
        yield return new DumpModule(this, module);
      }
    }

    /*
    public class DumpMemoryRead : IMemoryRead
    {
        public long Address { get; set; }
        public int BytesRequested { get; set; }
        public int BytesRead { get; set; }
        public DumpModule Module { get; set; }
        public FileSearchResult FileSearch { get; set; }
        public override string ToString()
        {
            return ToString("");
        }
        public string ToString(string format)
        {
            StringBuilder sb = new StringBuilder();
            if (BytesRead == 0)
                sb.Append("Failed ");
            else if (BytesRead < BytesRequested)
                sb.Append("Partial");
            else
                sb.Append("Success");
            sb.Append(string.Format(" - 0x{0,-16:x}: 0x{1,-8:x} of 0x{2,-8:x} bytes read", Address, BytesRead, BytesRequested));
            if (format == "detailed")
            {
                sb.AppendLine();
                string source = "Dump memory";
                if (Module != null)
                    source = string.Format("Image {0} (0x{1:x} - 0x{2:x})", Path.GetFileName(Module.FullName),
                        Module.BaseAddress, Module.BaseAddress + Module.Size);
                sb.AppendLine("Source: " + source);
                if (FileSearch != null && FileSearch.Path == null)
                {
                    sb.AppendLine("Image search failed:");
                    sb.AppendLine(FileSearch.ToString());
                }
            }
            return sb.ToString();
        }
    }
    */
  } // DumpReader
}