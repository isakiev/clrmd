using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Simple
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct DumpHeader
  {
    public readonly uint Singature;
    public readonly uint Version;
    public readonly uint NumberOfStreams;
    private readonly uint StreamDirectoryOffset;
    public readonly uint CheckSum;
    public readonly uint TimeDateStamp;
    public readonly ulong Flags;

    public ContentPosition StreamDirectoryPosition => new ContentPosition(StreamDirectoryOffset);
  }
}