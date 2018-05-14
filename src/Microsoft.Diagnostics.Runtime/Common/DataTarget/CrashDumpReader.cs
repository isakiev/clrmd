﻿namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   The type of crash dump reader to use.
  /// </summary>
  public enum CrashDumpReader
  {
    /// <summary>
    ///   Use DbgEng.  This allows the user to obtain an instance of IDebugClient through the
    ///   DataTarget.DebuggerInterface property, at the cost of strict threading requirements.
    /// </summary>
    DbgEng,

    /// <summary>
    ///   Use a simple dump reader to read data out of the crash dump.  This allows processing
    ///   multiple dumps (using separate DataTargets) on multiple threads, but the
    ///   DataTarget.DebuggerInterface property will return null.
    /// </summary>
    ClrMD
  }
}