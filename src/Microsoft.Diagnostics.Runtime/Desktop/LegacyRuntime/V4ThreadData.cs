﻿using System;

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V4ThreadData : IThreadData
  {
    public uint corThreadId;
    public uint osThreadId;
    public int state;
    public uint preemptiveGCDisabled;
    public ulong allocContextPtr;
    public ulong allocContextLimit;
    public ulong context;
    public ulong domain;
    public ulong pFrame;
    public uint lockCount;
    public ulong firstNestedException;
    public ulong teb;
    public ulong fiberData;
    public ulong lastThrownObjectHandle;
    public ulong nextThread;

    public ulong Next => IntPtr.Size == 8 ? nextThread : (uint)nextThread;

    public ulong AllocPtr => IntPtr.Size == 8 ? allocContextPtr : (uint)allocContextPtr;

    public ulong AllocLimit => IntPtr.Size == 8 ? allocContextLimit : (uint)allocContextLimit;

    public uint OSThreadID => osThreadId;

    public ulong Teb => IntPtr.Size == 8 ? teb : (uint)teb;

    public ulong AppDomain => domain;

    public uint LockCount => lockCount;

    public int State => state;

    public ulong ExceptionPtr => lastThrownObjectHandle;

    public uint ManagedThreadID => corThreadId;

    public bool Preemptive => preemptiveGCDisabled == 0;
  }
}