using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  /// <summary>
  ///   Utility class to provide various random Native debugging operations.
  /// </summary>
  internal static class DumpUtility
  {
    [StructLayout(LayoutKind.Explicit)]
    // See http://msdn.microsoft.com/msdnmag/issues/02/02/PE/default.aspx for more details

    // The only value of this is to get to at the IMAGE_NT_HEADERS.
    private struct IMAGE_DOS_HEADER
    {
      // DOS .EXE header
      [FieldOffset(0)]
      public readonly short e_magic; // Magic number

      /// <summary>
      ///   Determine if this is a valid DOS image.
      /// </summary>
      public bool IsValid => e_magic == 0x5a4d;
      // This is the offset of the IMAGE_NT_HEADERS, which is what we really want.
      [FieldOffset(0x3c)]
      public readonly uint e_lfanew; // File address of new exe header
    }

    [StructLayout(LayoutKind.Sequential)]

    // Native import for IMAGE_FILE_HEADER.
    private struct IMAGE_FILE_HEADER
    {
      public readonly short Machine;
      public readonly short NumberOfSections;
      public readonly uint TimeDateStamp;
      public readonly uint PointerToSymbolTable;
      public readonly uint NumberOfSymbols;
      public readonly short SizeOfOptionalHeader;
      public readonly short Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]

    // Native import for IMAGE_NT_HEADERs. 
    private struct IMAGE_NT_HEADERS
    {
      public readonly uint Signature;
      public readonly IMAGE_FILE_HEADER FileHeader;

      // Not marshalled.
      //IMAGE_OPTIONAL_HEADER OptionalHeader;
    }

    /// <summary>
    ///   Marshal a structure from the given buffer. Effectively returns ((T*) &amp;buffer[offset]).
    /// </summary>
    /// <typeparam name="T">type of structure to marshal</typeparam>
    /// <param name="buffer">array of bytes representing binary buffer to marshal</param>
    /// <param name="offset">offset in buffer to marhsal from</param>
    /// <returns>marshaled structure</returns>
    private static T MarshalAt<T>(byte[] buffer, uint offset)
    {
      // Ensure we have enough size in the buffer to copy from.
      var size = Marshal.SizeOf(typeof(T));
      if (offset + size > buffer.Length) throw new ArgumentOutOfRangeException();

      var h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
      var ptr = h.AddrOfPinnedObject();
      var p2 = new IntPtr(ptr.ToInt64() + offset);
      var header = default(T);
      Marshal.PtrToStructure(p2, header);

      h.Free();

      return header;
    }

    /// <summary>
    ///   Gets the raw compilation timestamp of a file.
    ///   This can be matched with the timestamp of a module in a dump file.
    ///   NOTE: This is NOT the same as the file's creation or last-write time.
    /// </summary>
    /// <param name="file"></param>
    /// <returns>
    ///   0 for common failures like file not found or invalid format. Throws on gross
    ///   errors. Else returns the module's timestamp for comparison against the minidump
    ///   module's stamp.
    /// </returns>
    public static uint GetTimestamp(string file)
    {
      if (!File.Exists(file)) return 0;

      var buffer = File.ReadAllBytes(file);

      var dos = MarshalAt<IMAGE_DOS_HEADER>(buffer, 0);
      if (!dos.IsValid)
        return 0;

      var idx = dos.e_lfanew;
      var header = MarshalAt<IMAGE_NT_HEADERS>(buffer, idx);

      var f = header.FileHeader;

      return f.TimeDateStamp;
    }
  }
}