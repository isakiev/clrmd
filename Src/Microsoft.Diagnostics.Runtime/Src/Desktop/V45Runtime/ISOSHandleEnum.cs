using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("3E269830-4A2B-4301-8EE2-D6805B29B2FA")]
  internal interface ISOSHandleEnum
  {
    void Skip(uint count);
    void Reset();
    void GetCount(out uint count);

    [PreserveSig]
    int Next(
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray)]
      HandleData[] handles,
      out uint pNeeded);
  }
}