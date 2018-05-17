using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("8FA642BD-9F10-4799-9AA3-512AE78C77EE")]
  internal interface ISOSStackRefEnum
  {
    void Skip(uint count);
    void Reset();
    void GetCount(out uint count);

    [PreserveSig]
    int Next(
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray)]
      StackRefData[] handles,
      out uint pNeeded);
  }
}