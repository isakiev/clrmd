﻿using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  /// Provides information about CLR's threadpool.
  /// </summary>
  public abstract class ClrThreadPool
  {
    /// <summary>
    /// The total number of threadpool worker threads in the process.
    /// </summary>
    public abstract int TotalThreads { get; }

    /// <summary>
    /// The number of running threadpool threads in the process.
    /// </summary>
    public abstract int RunningThreads { get; }

    /// <summary>
    /// The number of idle threadpool threads in the process.
    /// </summary>
    public abstract int IdleThreads { get; }

    /// <summary>
    /// The minimum number of threadpool threads allowable.
    /// </summary>
    public abstract int MinThreads { get; }

    /// <summary>
    /// The maximum number of threadpool threads allowable.
    /// </summary>
    public abstract int MaxThreads { get; }

    /// <summary>
    /// Returns the minimum number of completion ports (if any).
    /// </summary>
    public abstract int MinCompletionPorts { get; }

    /// <summary>
    /// Returns the maximum number of completion ports.
    /// </summary>
    public abstract int MaxCompletionPorts { get; }

    /// <summary>
    /// Returns the CPU utilization of the threadpool (as a percentage out of 100).
    /// </summary>
    public abstract int CpuUtilization { get; }

    /// <summary>
    /// The number of free completion port threads.
    /// </summary>
    public abstract int FreeCompletionPortCount { get; }

    /// <summary>
    /// The maximum number of free completion port threads.
    /// </summary>
    public abstract int MaxFreeCompletionPorts { get; }

    /// <summary>
    /// Enumerates the work items on the threadpool (native side).
    /// </summary>
    public abstract IEnumerable<NativeWorkItem> EnumerateNativeWorkItems();

    /// <summary>
    /// Enumerates work items on the thread pool (managed side).
    /// </summary>
    /// <returns></returns>
    public abstract IEnumerable<ManagedWorkItem> EnumerateManagedWorkItems();
  }
}