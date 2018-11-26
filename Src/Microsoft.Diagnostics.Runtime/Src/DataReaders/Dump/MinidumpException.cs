﻿using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  /// <summary>
  /// The struct that holds an EXCEPTION_RECORD
  /// </summary>
  [StructLayout(LayoutKind.Sequential)]
  internal class MINIDUMP_EXCEPTION
  {
    public uint ExceptionCode;
    public uint ExceptionFlags;
    public ulong ExceptionRecord;

    private ulong _exceptionaddress;
    public ulong ExceptionAddress
    {
      get => DumpNative.ZeroExtendAddress(_exceptionaddress);
      set => _exceptionaddress = value;
    }

    public uint NumberParameters;
    public uint __unusedAlignment;
    public ulong[] ExceptionInformation;

    public MINIDUMP_EXCEPTION()
    {
      ExceptionInformation = new ulong[DumpNative.EXCEPTION_MAXIMUM_PARAMETERS];
    }
  }
}