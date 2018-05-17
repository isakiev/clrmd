using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  internal struct IMAGE_NT_HEADERS
  {
    public uint Signature;
    public ImageFileHeader FileHeader;
  }
}