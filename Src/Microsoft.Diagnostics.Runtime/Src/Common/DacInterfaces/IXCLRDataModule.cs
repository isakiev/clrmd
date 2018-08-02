using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("88E32849-0A0A-4cb0-9022-7CD2E9E139E2")]
  internal interface IXCLRDataModule
  {
    /*
     * Enumerate assemblies this module is part of.
     * Module-to-assembly is an enumeration as a
     * shared module might be part of more than one assembly.
     */
    void StartEnumAssemblies_do_not_use( /*[out] CLRDATA_ENUM* handle*/);
    void EnumAssembly_do_not_use( /*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAssembly **assembly*/);
    void EndEnumAssemblies_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    /*
     * Enumerate types in this module.
     */
    void StartEnumTypeDefinitions_do_not_use( /*[out] CLRDATA_ENUM* handle*/);
    void EnumTypeDefinition_do_not_use( /*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataTypeDefinition **typeDefinition*/);
    void EndEnumTypeDefinitions_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    void StartEnumTypeInstances_do_not_use( /*[in] IXCLRDataAppDomain* appDomain, [out] CLRDATA_ENUM* handle*/);
    void EnumTypeInstance_do_not_use( /*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataTypeInstance **typeInstance*/);
    void EndEnumTypeInstances_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    /*
     * Look up types by name.
     */
    void StartEnumTypeDefinitionsByName_do_not_use( /*[in] LPCWSTR name, [in] ULONG32 flags, [out] CLRDATA_ENUM* handle*/);
    void EnumTypeDefinitionByName_do_not_use( /*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataTypeDefinition** type*/);
    void EndEnumTypeDefinitionsByName_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    void StartEnumTypeInstancesByName_do_not_use( /*[in] LPCWSTR name, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [out] CLRDATA_ENUM* handle*/);
    void EnumTypeInstanceByName_do_not_use( /*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataTypeInstance** type*/);
    void EndEnumTypeInstancesByName_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    /*
     * Get a type definition by metadata token.
     */
    void GetTypeDefinitionByToken_do_not_use( /*[in] mdTypeDef token, [out] IXCLRDataTypeDefinition** typeDefinition*/);

    /*
     * Look up methods by name.
     */
    void StartEnumMethodDefinitionsByName_do_not_use( /*[in] LPCWSTR name, [in] ULONG32 flags, [out] CLRDATA_ENUM* handle*/);
    void EnumMethodDefinitionByName_do_not_use( /*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataMethodDefinition** method*/);
    void EndEnumMethodDefinitionsByName_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    void StartEnumMethodInstancesByName_do_not_use( /*[in] LPCWSTR name, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [out] CLRDATA_ENUM* handle*/);
    void EnumMethodInstanceByName_do_not_use( /*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataMethodInstance** method*/);
    void EndEnumMethodInstancesByName_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    /*
     * Get a method definition by metadata token.
     */
    void GetMethodDefinitionByToken_do_not_use( /*[in] mdMethodDef token, [out] IXCLRDataMethodDefinition** methodDefinition*/);

    /*
     * Look up pieces of data by name.
     */
    void StartEnumDataByName_do_not_use( /*[in] LPCWSTR name, [in] ULONG32 flags, [in] IXCLRDataAppDomain* appDomain, [in] IXCLRDataTask* tlsTask, [out] CLRDATA_ENUM* handle*/);
    void EnumDataByName_do_not_use( /*[in,out] CLRDATA_ENUM* handle, [out] IXCLRDataValue** value*/);
    void EndEnumDataByName_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    /*
     * Get the module's base name.
     */
    void GetName_do_not_use( /*[in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR name[]*/);

    /*
     * Get the full path and filename for the module,
     * if there is one.
     */
    void GetFileName_do_not_use( /*[in] ULONG32 bufLen, [out] ULONG32 *nameLen, [out, size_is(bufLen)] WCHAR name[]*/);

    /*
     * Get state flags, defined in CLRDataModuleFlag.
     */
    void GetFlags_do_not_use( /*[out] ULONG32* flags*/);

    /*
     * Determine whether the given interface represents
     * the same target state.
     */
    void IsSameObject_do_not_use( /*[in] IXCLRDataModule* mod*/);

    /*
     * Get the memory regions associated with this module.
     */
    void StartEnumExtents(out ulong handle);
    void EnumExtent(ref ulong handle, out CLRDATA_MODULE_EXTENT extent);
    void EndEnumExtents(ulong handle);

    void Request_do_not_use(
      /*[in] ULONG32 reqCode, [in] ULONG32 inBufferSize, [in, size_is(inBufferSize)] BYTE* inBuffer, [in] ULONG32 outBufferSize, [out, size_is(outBufferSize)] BYTE* outBuffer*/);

    /*
     * Enumerate the app domains using this module.
     */
    void StartEnumAppDomains_do_not_use( /*[out] CLRDATA_ENUM* handle*/);
    void EnumAppDomain_do_not_use( /*[in, out] CLRDATA_ENUM* handle, [out] IXCLRDataAppDomain** appDomain*/);
    void EndEnumAppDomains_do_not_use( /*[in] CLRDATA_ENUM handle*/);

    /*
     * Get the module's version ID.
     * Requires revision 3.
     */
    void GetVersionId_do_not_use( /*[out] GUID* vid*/);
  }
}