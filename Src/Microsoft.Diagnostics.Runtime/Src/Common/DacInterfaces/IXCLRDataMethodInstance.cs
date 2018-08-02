using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("ECD73800-22CA-4b0d-AB55-E9BA7E6318A5")]
  internal interface IXCLRDataMethodInstance
  {
    void GetTypeInstance_do_not_use( /*[out] IXCLRDataTypeInstance **typeInstance*/);
    void GetDefinition_do_not_use( /*[out] IXCLRDataMethodDefinition **methodDefinition*/);

    /*
     * Get the metadata token and scope.
     */
    void GetTokenAndScope(
      out uint mdToken,
      [Out][MarshalAs(UnmanagedType.Interface)]
      out object module);

    void GetName_do_not_use(
      /*[in] ULONG32 flags,
                                  [in] ULONG32 bufLen,
                                  [out] ULONG32 *nameLen,
                                  [out, size_is(bufLen)] WCHAR nameBuf[]*/);

    void GetFlags_do_not_use( /*[out] ULONG32* flags*/);
    void IsSameObject_do_not_use( /*[in] IXCLRDataMethodInstance* method*/);
    void GetEnCVersion_do_not_use( /*[out] ULONG32* version*/);
    void GetNumTypeArguments_do_not_use( /*[out] ULONG32* numTypeArgs*/);

    void GetTypeArgumentByIndex_do_not_use(
      /*[in] ULONG32 index,
                                                 [out] IXCLRDataTypeInstance** typeArg*/);

    /*
     * Access the IL <-> address mapping information.
     */
    void GetILOffsetsByAddress(
      ulong address,
      uint offsetsLen,
      out uint offsetsNeeded,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      uint[] ilOffsets);

    void GetAddressRangesByILOffset(
      uint ilOffset,
      uint rangesLen,
      out uint rangesNeeded,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      uint[] addressRanges);

    [PreserveSig]
    int GetILAddressMap(
      uint mapLen,
      out uint mapNeeded,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
      ILToNativeMap[] map);

    void StartEnumExtents_do_not_use( /*[out] CLRDATA_ENUM* handle*/);

    void EnumExtent_do_not_use(
      /*[in, out] CLRDATA_ENUM* handle,
                                     [out] CLRDATA_ADDRESS_RANGE* extent*/);

    void EndEnumExtents_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    void Request_do_not_use(
      /*[in] ULONG32 reqCode,
                                  [in] ULONG32 inBufferSize,
                                  [in, size_is(inBufferSize)] BYTE* inBuffer,
                                  [in] ULONG32 outBufferSize,
                                  [out, size_is(outBufferSize)] BYTE* outBuffer*/);

    void GetRepresentativeEntryAddress_do_not_use( /*[out] CLRDATA_ADDRESS* addr*/);
  }
}