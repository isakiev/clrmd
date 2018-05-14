using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Diagnostics.Runtime
{
  internal class NativeMethods
  {
    public static bool LoadNative(string dllName)
    {
      return LoadLibrary(dllName) != IntPtr.Zero;
    }

    private const string Kernel32LibraryName = "kernel32.dll";

    public const uint FILE_MAP_READ = 4;

    // Call CloseHandle to clean up.
    [DllImport(Kernel32LibraryName, SetLastError = true)]
    public static extern SafeWin32Handle CreateFileMapping(
      SafeFileHandle hFile,
      IntPtr lpFileMappingAttributes,
      PageProtection flProtect,
      uint dwMaximumSizeHigh,
      uint dwMaximumSizeLow,
      string lpName);

    [DllImport(Kernel32LibraryName, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnmapViewOfFile(IntPtr baseAddress);

    [DllImport(Kernel32LibraryName, SetLastError = true)]
    public static extern SafeMapViewHandle MapViewOfFile(
      SafeWin32Handle hFileMappingObject,
      uint
        dwDesiredAccess,
      uint dwFileOffsetHigh,
      uint dwFileOffsetLow,
      IntPtr dwNumberOfBytesToMap);

    [DllImport(Kernel32LibraryName)]
    public static extern void RtlMoveMemory(IntPtr destination, IntPtr source, IntPtr numberBytes);

    [DllImport(Kernel32LibraryName, SetLastError = true, PreserveSig = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport(Kernel32LibraryName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeLibrary(IntPtr hModule);

    public static IntPtr LoadLibrary(string lpFileName)
    {
      return LoadLibraryEx(lpFileName, 0, LoadLibraryFlags.NoFlags);
    }

    [DllImport(Kernel32LibraryName, SetLastError = true)]
    public static extern IntPtr LoadLibraryEx(string fileName, int hFile, LoadLibraryFlags dwFlags);

    [Flags]
    public enum LoadLibraryFlags : uint
    {
      NoFlags = 0x00000000,
      DontResolveDllReferences = 0x00000001,
      LoadIgnoreCodeAuthzLevel = 0x00000010,
      LoadLibraryAsDatafile = 0x00000002,
      LoadLibraryAsDatafileExclusive = 0x00000040,
      LoadLibraryAsImageResource = 0x00000020,
      LoadWithAlteredSearchPath = 0x00000008
    }

    [Flags]
    public enum PageProtection : uint
    {
      NoAccess = 0x01,
      Readonly = 0x02,
      ReadWrite = 0x04,
      WriteCopy = 0x08,
      Execute = 0x10,
      ExecuteRead = 0x20,
      ExecuteReadWrite = 0x40,
      ExecuteWriteCopy = 0x80,
      Guard = 0x100,
      NoCache = 0x200,
      WriteCombine = 0x400
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool isWow64);

    [DllImport("version.dll")]
    internal static extern bool GetFileVersionInfo(string sFileName, int handle, int size, byte[] infoBuffer);

    [DllImport("version.dll")]
    internal static extern int GetFileVersionInfoSize(string sFileName, out int handle);

    [DllImport("version.dll")]
    internal static extern bool VerQueryValue(byte[] pBlock, string pSubBlock, out IntPtr val, out int len);

    private const int VS_FIXEDFILEINFO_size = 0x34;
    public static short IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR = 14;

    [DefaultDllImportSearchPaths(DllImportSearchPath.LegacyBehavior)]
    [DllImport("dbgeng.dll")]
    internal static extern uint DebugCreate(ref Guid InterfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object Interface);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int CreateDacInstance(
      [In][ComAliasName("REFIID")] ref Guid riid,
      [In][MarshalAs(UnmanagedType.Interface)]
      IDacDataTarget data,
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object ppObj);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

    internal static bool IsEqualFileVersion(string file, VersionInfo version)
    {
      if (!GetFileVersion(file, out var major, out var minor, out var revision, out var patch))
        return false;

      return major == version.Major && minor == version.Minor && revision == version.Revision && patch == version.Patch;
    }

    internal static bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch)
    {
      major = minor = revision = patch = 0;

      var len = GetFileVersionInfoSize(dll, out var handle);
      if (len <= 0)
        return false;

      var data = new byte[len];
      if (!GetFileVersionInfo(dll, handle, len, data))
        return false;

      if (!VerQueryValue(data, "\\", out var ptr, out len))
        return false;

      var vsFixedInfo = new byte[len];
      Marshal.Copy(ptr, vsFixedInfo, 0, len);

      minor = (ushort)Marshal.ReadInt16(vsFixedInfo, 8);
      major = (ushort)Marshal.ReadInt16(vsFixedInfo, 10);
      patch = (ushort)Marshal.ReadInt16(vsFixedInfo, 12);
      revision = (ushort)Marshal.ReadInt16(vsFixedInfo, 14);

      return true;
    }

    internal static bool TryGetWow64(IntPtr proc, out bool result)
    {
      if (Environment.OSVersion.Version.Major > 5 ||
        Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1)
      {
        return IsWow64Process(proc, out result);
      }

      result = false;
      return false;
    }
  }
}