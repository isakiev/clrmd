using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct RVA
  {
    public uint Value;

    public bool IsNull => Value == 0;
  }
}