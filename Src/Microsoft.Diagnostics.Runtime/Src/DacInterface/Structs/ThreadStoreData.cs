﻿using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
  [StructLayout(LayoutKind.Sequential)]
  public readonly struct ThreadStoreData : IThreadStoreData
  {
    public readonly int ThreadCount;
    public readonly int UnstartedThreadCount;
    public readonly int BackgroundThreadCount;
    public readonly int PendingThreadCount;
    public readonly int DeadThreadCount;
    public readonly ulong FirstThread;
    public readonly ulong FinalizerThread;
    public readonly ulong GCThread;
    public readonly uint HostConfig;

    ulong IThreadStoreData.Finalizer => FinalizerThread;
    int IThreadStoreData.Count => ThreadCount;
    ulong IThreadStoreData.FirstThread => FirstThread;
  }
}