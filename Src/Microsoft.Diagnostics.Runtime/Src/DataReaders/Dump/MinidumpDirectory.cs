using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct MINIDUMP_DIRECTORY
  {
    public readonly MINIDUMP_STREAM_TYPE StreamType;
    public readonly uint DataSize;
    public readonly uint Rva;
  }
}