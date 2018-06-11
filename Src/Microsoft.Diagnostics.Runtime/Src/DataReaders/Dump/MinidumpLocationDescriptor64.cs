﻿using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  /// <summary>
  ///   Describes a data stream within the minidump
  /// </summary>
  [StructLayout(LayoutKind.Sequential)]
  internal struct MINIDUMP_LOCATION_DESCRIPTOR64
  {
    /// <summary>
    ///   Size of the stream in bytes.
    /// </summary>
    public ulong DataSize;

    /// <summary>
    ///   Offset (in bytes) from the start of the minidump to the data stream.
    /// </summary>
    public RVA64 Rva;
  }
}