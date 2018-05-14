using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.Runtime
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("3E11CCEE-D08B-43e5-AF01-32717A64DA03")]
  internal interface IDacDataTarget
  {
    void GetMachineType(out IMAGE_FILE_MACHINE machineType);

    void GetPointerSize(out uint pointerSize);

    void GetImageBase([In][MarshalAs(UnmanagedType.LPWStr)] string imagePath, out ulong baseAddress);

    [PreserveSig]
    int ReadVirtual(
      ulong address,
      IntPtr buffer,
      int bytesRequested,
      out int bytesRead);

    void WriteVirtual(
      ulong address,
      [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
      byte[] buffer,
      uint bytesRequested,
      out uint bytesWritten);

    void GetTLSValue(
      uint threadID,
      uint index,
      out ulong value);

    void SetTLSValue(
      uint threadID,
      uint index,
      ulong value);

    void GetCurrentThreadID(out uint threadID);

    void GetThreadContext(
      uint threadID,
      uint contextFlags,
      uint contextSize,
      IntPtr context);

    void SetThreadContext(
      uint threadID,
      uint contextSize,
      IntPtr context);

    void Request(
      uint reqCode,
      uint inBufferSize,
      IntPtr inBuffer,
      IntPtr outBufferSize,
      out IntPtr outBuffer);
  }
}