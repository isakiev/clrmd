﻿namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   The COM implementation details of a single CCW entry.
  /// </summary>
  public abstract class ComInterfaceData
  {
    /// <summary>
    ///   The CLR type this represents.
    /// </summary>
    public abstract ClrType Type { get; }

    /// <summary>
    ///   The interface pointer of Type.
    /// </summary>
    public abstract ulong InterfacePointer { get; }
  }
}