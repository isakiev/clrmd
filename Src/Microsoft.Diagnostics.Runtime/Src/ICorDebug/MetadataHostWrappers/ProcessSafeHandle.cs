﻿//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------

using System;
using Microsoft.Win32.SafeHandles;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
  internal class ProcessSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
  {
    private ProcessSafeHandle()
      : base(true)
    {
    }

    private ProcessSafeHandle(IntPtr handle, bool ownsHandle)
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