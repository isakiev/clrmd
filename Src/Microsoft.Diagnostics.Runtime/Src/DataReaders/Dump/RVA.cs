using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct RVA
  {
    public uint Value;

    public bool IsNull => Value == 0;
  }
}