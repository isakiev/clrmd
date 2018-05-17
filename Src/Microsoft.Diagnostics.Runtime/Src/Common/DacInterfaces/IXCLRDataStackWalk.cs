using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("E59D8D22-ADA7-49a2-89B5-A415AFCFC95F")]
  internal interface IXCLRDataStackWalk
  {
    [PreserveSig]
    int GetContext(
      uint contextFlags,
      uint contextBufSize,
      out uint contextSize,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      byte[] buffer);

    void SetContext_do_not_use();

    [PreserveSig]
    int Next();

    /*
     * Return the number of bytes skipped by the last call to Next().
     * If Next() moved to the very next frame, outputs 0.
     *
     * Note that calling GetStackSizeSkipped() after any function other
     * than Next() has no meaning.
     */
    void GetStackSizeSkipped_do_not_use();

    /* 
     * Return information about the type of the current frame
     */
    void GetFrameType_do_not_use();

    [PreserveSig]
    int GetFrame(
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object frame);

    [PreserveSig]
    int Request(
      uint reqCode,
      uint inBufferSize,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      byte[] inBuffer,
      uint outBufferSize,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
      byte[] outBuffer);

    void SetContext2_do_not_use();
  }
}