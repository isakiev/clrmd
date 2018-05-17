using System;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Diagnostics.Runtime
{
  internal sealed class SafeMapViewHandle : SafeHandleZeroOrMinusOneIsInvalid
  {
    private SafeMapViewHandle() : base(true)
    {
    }

    protected override bool ReleaseHandle()
    {
      return NativeMethods.UnmapViewOfFile(handle);
    }

    // This is technically equivalent to DangerousGetHandle, but it's safer for file
    // mappings. In file mappings, the "handle" is actually a base address that needs
    // to be used in computations and RVAs.
    // So provide a safer accessor method.
    public IntPtr BaseAddress => handle;
  }
}