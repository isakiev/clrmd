﻿using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Diagnostics.Runtime
{
  internal sealed class SafeLoadLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
  {
    private SafeLoadLibraryHandle() : base(true)
    {
    }

    public SafeLoadLibraryHandle(IntPtr handle)
      : base(true)
    {
      SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
      return FreeLibrary(handle);
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hModule);

    // This is technically equivalent to DangerousGetHandle, but it's safer for loaded
    // libraries where the HMODULE is also the base address the module is loaded at.
    public IntPtr BaseAddress => handle;
  }
}