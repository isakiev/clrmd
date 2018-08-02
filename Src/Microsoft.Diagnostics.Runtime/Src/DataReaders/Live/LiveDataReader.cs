﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Live
{
  public unsafe class LiveDataReader : IDataReader
  {
    private readonly int _originalPid;
    private readonly IntPtr _snapshotHandle;
    private readonly IntPtr _cloneHandle;
    private IntPtr _process;
    private readonly int _pid;

    private const int PROCESS_VM_READ = 0x10;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;

    public LiveDataReader(int pid, bool createSnapshot)
    {
      if (createSnapshot)
      {
        _originalPid = pid;
        var process = Process.GetProcessById(pid);
        var hr = PssCaptureSnapshot(process.Handle, PssCaptureFlags.PSS_CAPTURE_VA_CLONE, IntPtr.Size == 8 ? 0x0010001F : 0x0001003F, out _snapshotHandle);
        if (hr != 0) throw new ClrDiagnosticsException(string.Format("Could not create snapshot to process. Error {0}.", hr));

        hr = PssQuerySnapshot(_snapshotHandle, PssQueryInformationClass.PSS_QUERY_VA_CLONE_INFORMATION, out _cloneHandle, IntPtr.Size);
        if (hr != 0) throw new ClrDiagnosticsException(string.Format("Could not query the snapshot. Error {0}.", hr));

        _pid = GetProcessId(_cloneHandle);
      }
      else
      {
        _pid = pid;
      }

      _process = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _pid);

      if (_process == IntPtr.Zero)
        throw new ClrDiagnosticsException(string.Format("Could not attach to process. Error {0}.", Marshal.GetLastWin32Error()));

      using (var p = Process.GetCurrentProcess())
      {
        if (NativeMethods.TryGetWow64(p.Handle, out var wow64) &&
          NativeMethods.TryGetWow64(_process, out var targetWow64) &&
          wow64 != targetWow64)
          throw new ClrDiagnosticsException("Dac architecture mismatch!");
      }
    }

    public bool IsMinidump => false;

    public void Close()
    {
      if (_originalPid != 0)
      {
        CloseHandle(_cloneHandle);
        var hr = PssFreeSnapshot(Process.GetCurrentProcess().Handle, _snapshotHandle);
        if (hr != 0) throw new ClrDiagnosticsException(string.Format("Could not free the snapshot. Error {0}.", hr));

        try
        {
          Process.GetProcessById(_pid).Kill();
        }
        catch (Win32Exception)
        {
        }
      }

      if (_process != IntPtr.Zero)
      {
        CloseHandle(_process);
        _process = IntPtr.Zero;
      }
    }

    public void Flush()
    {
    }

    public Architecture GetArchitecture()
    {
      if (IntPtr.Size == 4)
        return Architecture.X86;

      return Architecture.Amd64;
    }

    public uint GetPointerSize()
    {
      return (uint)IntPtr.Size;
    }

    public IList<ModuleInfo> EnumerateModules()
    {
      var result = new List<ModuleInfo>();

      EnumProcessModules(_process, null, 0, out var needed);

      var modules = new IntPtr[needed / 4];
      var size = (uint)modules.Length * sizeof(uint);

      if (!EnumProcessModules(_process, modules, size, out needed))
        throw new ClrDiagnosticsException("Unable to get process modules.", ClrDiagnosticsExceptionKind.DataRequestError);

      for (var i = 0; i < modules.Length; i++)
      {
        var ptr = modules[i];

        if (ptr == IntPtr.Zero) break;

        var sb = new StringBuilder(1024);
        GetModuleFileNameExA(_process, ptr, sb, sb.Capacity);

        var baseAddr = (ulong)ptr.ToInt64();
        GetFileProperties(baseAddr, out var filesize, out var timestamp);

        var filename = sb.ToString();
        var module = new ModuleInfo(this)
        {
          ImageBase = baseAddr,
          FileName = filename,
          FileSize = filesize,
          TimeStamp = timestamp
        };

        result.Add(module);
      }

      return result;
    }

    public void GetVersionInfo(ulong addr, out VersionInfo version)
    {
      var filename = new StringBuilder(1024);
      GetModuleFileNameExA(_process, new IntPtr((long)addr), filename, filename.Capacity);

      if (NativeMethods.GetFileVersion(filename.ToString(), out var major, out var minor, out var revision, out var patch))
        version = new VersionInfo(major, minor, revision, patch);
      else
        version = new VersionInfo();
    }

    public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
    {
      try
      {
        var res = ReadProcessMemory(_process, new IntPtr((long)address), buffer, bytesRequested, out bytesRead);
        return res != 0;
      }
      catch
      {
        bytesRead = 0;
        return false;
      }
    }

    public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
    {
      try
      {
        var res = RawPinvokes.ReadProcessMemory(_process, new IntPtr((long)address), buffer, bytesRequested, out bytesRead);
        return res != 0;
      }
      catch
      {
        bytesRead = 0;
        return false;
      }
    }

    private readonly byte[] _ptrBuffer = new byte[IntPtr.Size];

    public ulong ReadPointerUnsafe(ulong addr)
    {
      if (!ReadMemory(addr, _ptrBuffer, IntPtr.Size, out var read))
        return 0;

      fixed (byte* r = _ptrBuffer)
      {
        if (IntPtr.Size == 4)
          return *((uint*)r);

        return *((ulong*)r);
      }
    }

    public uint ReadDwordUnsafe(ulong addr)
    {
      if (!ReadMemory(addr, _ptrBuffer, 4, out var read))
        return 0;

      fixed (byte* r = _ptrBuffer)
      {
        return *((uint*)r);
      }
    }

    public ulong GetThreadTeb(uint thread)
    {
      // todo
      throw new NotImplementedException();
    }

    public IEnumerable<uint> EnumerateAllThreads()
    {
      var p = Process.GetProcessById(_pid);
      foreach (ProcessThread thread in p.Threads)
        yield return (uint)thread.Id;
    }

    public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
    {
      vq = new VirtualQueryData();

      var mem = new MemoryBasicInformation();
      var ptr = new IntPtr((long)addr);

      var res = VirtualQueryEx(_process, ptr, ref mem, new IntPtr(Marshal.SizeOf(mem)));
      if (res == 0)
        return false;

      vq.BaseAddress = mem.BaseAddress;
      vq.Size = mem.Size;
      return true;
    }

    public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
    {
      using (var thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID))
      {
        if (thread.IsInvalid)
          return false;

        var res = GetThreadContext(thread.DangerousGetHandle(), context);
        return res;
      }
    }

    public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
    {
      using (var thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID))
      {
        if (thread.IsInvalid)
          return false;

        fixed (byte* b = context)
        {
          var res = GetThreadContext(thread.DangerousGetHandle(), new IntPtr(b));
          return res;
        }
      }
    }

    private void GetFileProperties(ulong moduleBase, out uint filesize, out uint timestamp)
    {
      filesize = 0;
      timestamp = 0;
      var buffer = new byte[4];

      if (ReadMemory(moduleBase + 0x3c, buffer, buffer.Length, out var read) && read == buffer.Length)
      {
        var sigOffset = (uint)BitConverter.ToInt32(buffer, 0);
        var sigLength = 4;

        if (ReadMemory(moduleBase + sigOffset, buffer, buffer.Length, out read) && read == buffer.Length)
        {
          var header = (uint)BitConverter.ToInt32(buffer, 0);

          // Ensure the module contains the magic "PE" value at the offset it says it does.  This check should
          // never fail unless we have the wrong base address for CLR.
          Debug.Assert(header == 0x4550);
          if (header == 0x4550)
          {
            const int timeDataOffset = 4;
            const int imageSizeOffset = 0x4c;
            if (ReadMemory(moduleBase + sigOffset + (ulong)sigLength + timeDataOffset, buffer, buffer.Length, out read) && read == buffer.Length)
              timestamp = (uint)BitConverter.ToInt32(buffer, 0);

            if (ReadMemory(moduleBase + sigOffset + (ulong)sigLength + imageSizeOffset, buffer, buffer.Length, out read) && read == buffer.Length)
              filesize = (uint)BitConverter.ToInt32(buffer, 0);
          }
        }
      }
    }

    [DllImport("kernel32.dll", EntryPoint = "OpenProcess")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, [MarshalAs(UnmanagedType.U4)] out uint lpcbNeeded);

    [DllImport("psapi.dll", SetLastError = true)]
    [PreserveSig]
    public static extern uint GetModuleFileNameExA([In] IntPtr hProcess, [In] IntPtr hModule, [Out] StringBuilder lpFilename, [In][MarshalAs(UnmanagedType.U4)] int nSize);

    [DllImport("kernel32.dll")]
    private static extern int ReadProcessMemory(
      IntPtr hProcess,
      IntPtr lpBaseAddress,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
      byte[] lpBuffer,
      int dwSize,
      out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, ref MemoryBasicInformation lpBuffer, IntPtr dwLength);

    [DllImport("kernel32.dll")]
    private static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeWin32Handle OpenThread(ThreadAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32")]
    private static extern int PssCaptureSnapshot(IntPtr ProcessHandle, PssCaptureFlags CaptureFlags, int ThreadContextFlags, out IntPtr SnapshotHandle);

    [DllImport("kernel32")]
    private static extern int PssFreeSnapshot(IntPtr ProcessHandle, IntPtr SnapshotHandle);

    [DllImport("kernel32")]
    private static extern int PssQuerySnapshot(IntPtr SnapshotHandle, PssQueryInformationClass InformationClass, out IntPtr Buffer, int BufferLength);

    [DllImport("kernel32")]
    private static extern int GetProcessId(IntPtr hObject);
  }
}