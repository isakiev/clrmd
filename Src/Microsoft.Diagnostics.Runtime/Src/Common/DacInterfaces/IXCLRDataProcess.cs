using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7")]
  internal interface IXCLRDataProcess
  {
    void Flush();

    void StartEnumTasks_do_not_use();
    void EnumTask_do_not_use();
    void EndEnumTasks_do_not_use();

    [PreserveSig]
    int GetTaskByOSThreadID(
      uint id,
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object task);

    void GetTaskByUniqueID_do_not_use( /*[in] ULONG64 taskID, [out] IXCLRDataTask** task*/);
    void GetFlags_do_not_use( /*[out] ULONG32* flags*/);
    void IsSameObject_do_not_use( /*[in] IXCLRDataProcess* process*/);
    void GetManagedObject_do_not_use( /*[out] IXCLRDataValue** value*/);
    void GetDesiredExecutionState_do_not_use( /*[out] ULONG32* state*/);
    void SetDesiredExecutionState_do_not_use( /*[in] ULONG32 state*/);
    void GetAddressType_do_not_use( /*[in] CLRDATA_ADDRESS address, [out] CLRDataAddressType* type*/);

    void GetRuntimeNameByAddress_do_not_use(
      /*[in] CLRDATA_ADDRESS address, [in] ULONG32 flags, [in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR nameBuf[], [out] CLRDATA_ADDRESS* displacement*/);

    void StartEnumAppDomains_do_not_use( /*[out] CLRDATA_ENUM* handle*/);
    void EnumAppDomain_do_not_use( /*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAppDomain** appDomain*/);
    void EndEnumAppDomains_do_not_use( /*[in] CLRDATA_ENUM handle*/);
    void GetAppDomainByUniqueID_do_not_use( /*[in] ULONG64 id, [out] IXCLRDataAppDomain** appDomain*/);
    void StartEnumAssemblie_do_not_uses( /*[out] CLRDATA_ENUM* handle*/);
    void EnumAssembly_do_not_use( /*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAssembly **assembly*/);
    void EndEnumAssemblies_do_not_use( /*[in] CLRDATA_ENUM handle*/);
    void StartEnumModules_do_not_use( /*[out] CLRDATA_ENUM* handle*/);
    void EnumModule_do_not_use( /*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataModule **mod*/);
    void EndEnumModules_do_not_use( /*[in] CLRDATA_ENUM handle*/);
    void GetModuleByAddress_do_not_use( /*[in] CLRDATA_ADDRESS address, [out] IXCLRDataModule** mod*/);

    [PreserveSig]
    int StartEnumMethodInstancesByAddress(
      ulong address,
      [In][MarshalAs(UnmanagedType.Interface)]
      object appDomain,
      out ulong handle);

    [PreserveSig]
    int EnumMethodInstanceByAddress(
      ref ulong handle,
      [Out][MarshalAs(UnmanagedType.Interface)]
      out object method);

    [PreserveSig]
    int EndEnumMethodInstancesByAddress(ulong handle);

    void GetDataByAddress_do_not_use(
      /*[in] CLRDATA_ADDRESS address, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [in] IXCLRDataTask* tlsTask, [in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR nameBuf[], [out] IXCLRDataValue** value, [out] CLRDATA_ADDRESS* displacement*/);

    void GetExceptionStateByExceptionRecord_do_not_use( /*[in] EXCEPTION_RECORD64* record, [out] IXCLRDataExceptionState **exState*/);
    void TranslateExceptionRecordToNotification_do_not_use();

    [PreserveSig]
    int Request(
      uint reqCode,
      uint inBufferSize,
      [In][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      byte[] inBuffer,
      uint outBufferSize,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
      byte[] outBuffer);
  }
}