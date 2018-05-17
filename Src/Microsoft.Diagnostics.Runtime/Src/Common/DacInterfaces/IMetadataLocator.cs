using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("aa8fa804-bc05-4642-b2c5-c353ed22fc63")]
  internal interface IMetadataLocator
  {
    [PreserveSig]
    int GetMetadata(
      [In][MarshalAs(UnmanagedType.LPWStr)] string imagePath,
      uint imageTimestamp,
      uint imageSize,
      IntPtr mvid, // (guid, unused)
      uint mdRva,
      uint flags, // unused
      uint bufferSize,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 6)]
      byte[] buffer,
      IntPtr ptr);
  }
}