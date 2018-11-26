﻿using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
  internal unsafe class ClrDataTask : CallableCOMWrapper
  {
    private static Guid IID_IXCLRDataTask = new Guid("A5B0BEEA-EC62-4618-8012-A24FFC23934C");

    private ClrDataTaskVTable* VTable => (ClrDataTaskVTable*)_vtable;

    public ClrDataTask(DacLibrary library, IntPtr pUnk)
      : base(library.OwningLibrary, ref IID_IXCLRDataTask, pUnk)
    {
    }

    public ClrStackWalk CreateStackWalk(DacLibrary library, uint flags)
    {
      var create = (CreateStackWalkDelegate)Marshal.GetDelegateForFunctionPointer(VTable->CreateStackWalk, typeof(CreateStackWalkDelegate));
      var hr = create(Self, flags, out var pUnk);
      if (hr != S_OK)
        return null;

      return new ClrStackWalk(library, pUnk);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateStackWalkDelegate(IntPtr self, uint flags, out IntPtr stackwalk);
  }

#pragma warning disable CS0169
#pragma warning disable CS0649
  internal struct ClrDataTaskVTable
  {
    private readonly IntPtr GetProcess;
    private readonly IntPtr GetCurrentAppDomain;
    private readonly IntPtr GetUniqueID;
    private readonly IntPtr GetFlags;
    private readonly IntPtr IsSameObject;
    private readonly IntPtr GetManagedObject;
    private readonly IntPtr GetDesiredExecutionState;
    private readonly IntPtr SetDesiredExecutionState;
    public readonly IntPtr CreateStackWalk;
    private readonly IntPtr GetOSThreadID;
    private readonly IntPtr GetContext;
    private readonly IntPtr SetContext;
    private readonly IntPtr GetCurrentExceptionState;
    private readonly IntPtr Request;
    private readonly IntPtr GetName;
    private readonly IntPtr GetLastExceptionState;
  }
}