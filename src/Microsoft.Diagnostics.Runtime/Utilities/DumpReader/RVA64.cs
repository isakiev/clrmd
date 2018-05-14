using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct RVA64
  {
    public ulong Value;
  }
}