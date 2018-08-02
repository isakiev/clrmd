using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Simple
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct DumpStreamDetails
  {
    public readonly DumpStreamType Type;
    public readonly uint Size;
    private readonly uint Offset;
    
    public ContentPosition Position => new ContentPosition(Offset);
  }
}