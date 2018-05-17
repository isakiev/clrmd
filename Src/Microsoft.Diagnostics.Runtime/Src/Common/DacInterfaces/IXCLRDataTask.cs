using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("A5B0BEEA-EC62-4618-8012-A24FFC23934C")]
  internal interface IXCLRDataTask
  {
    void GetProcess_do_not_use();
    void GetCurrentAppDomain_do_not_use();
    void GetUniqueID_do_not_use();
    void GetFlags_do_not_use();
    void IsSameObject_do_not_use();
    void GetManagedObject_do_not_use();
    void GetDesiredExecutionState_do_not_use();
    void SetDesiredExecutionState_do_not_use();

    /*
     * Create a stack walker to walk this task's stack. The
     * flags parameter takes a bitfield of values from the
     * CLRDataSimpleFrameType enum.
     */
    [PreserveSig]
    int CreateStackWalk(
      uint flags,
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object stackwalk);

    void GetOSThreadID_do_not_use();
    void GetContext_do_not_use();
    void SetContext_do_not_use();
    void GetCurrentExceptionState_do_not_use();
    void Request_do_not_use();
    void GetName_do_not_use();
    void GetLastExceptionState_do_not_use();
  }
}