using System;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Diagnostics.Runtime
{
  internal sealed class SafeWin32Handle : SafeHandleZeroOrMinusOneIsInvalid
  {
    public SafeWin32Handle() : base(true)
    {
    }

    public SafeWin32Handle(IntPtr handle)
      : this(handle, true)
    {
    }

    public SafeWin32Handle(IntPtr handle, bool ownsHandle)
      : base(ownsHandle)
    {
      SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
      return NativeMethods.CloseHandle(handle);
    }
  }
}