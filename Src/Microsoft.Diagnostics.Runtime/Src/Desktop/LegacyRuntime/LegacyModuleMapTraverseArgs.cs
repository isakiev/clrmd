using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  // Same for v2 and v4
  [StructLayout(LayoutKind.Sequential)]
  internal struct LegacyModuleMapTraverseArgs
  {
    private readonly uint _setToZero;
    public ulong module;
    public IntPtr pCallback;
    public IntPtr token;
  }
}