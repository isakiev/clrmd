using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct RVA64
  {
    public ulong Value;
  }
}