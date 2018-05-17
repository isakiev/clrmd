using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  [ComImport]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [Guid("436f00f2-b42a-4b9f-870c-e73db66ae930")]
  internal interface ISOSDac
  {
    // ThreadStore
    [PreserveSig]
    int GetThreadStoreData(out LegacyThreadStoreData data);

    // AppDomains
    [PreserveSig]
    int GetAppDomainStoreData(out LegacyAppDomainStoreData data);

    [PreserveSig]
    int GetAppDomainList(
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
      ulong[] values,
      out uint pNeeded);

    [PreserveSig]
    int GetAppDomainData(ulong addr, out LegacyAppDomainData data);

    [PreserveSig]
    int GetAppDomainName(ulong addr, uint count, [Out] StringBuilder lpFilename, out uint pNeeded);

    [PreserveSig]
    int GetDomainFromContext(ulong context, out ulong domain);

    // Assemblies
    [PreserveSig]
    int GetAssemblyList(
      ulong appDomain,
      int count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      ulong[] values,
      out int pNeeded);

    [PreserveSig]
    int GetAssemblyData(ulong baseDomainPtr, ulong assembly, out LegacyAssemblyData data);

    [PreserveSig]
    int GetAssemblyName(ulong assembly, uint count, [Out] StringBuilder name, out uint pNeeded);

    // Modules
    [PreserveSig]
    int GetModule(
      ulong addr,
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object module);

    [PreserveSig]
    int GetModuleData(ulong moduleAddr, out V45ModuleData data);

    [PreserveSig]
    int TraverseModuleMap(
      int mmt,
      ulong moduleAddr,
      [In][MarshalAs(UnmanagedType.FunctionPtr)]
      ModuleMapTraverse pCallback,
      IntPtr token);

    [PreserveSig]
    int GetAssemblyModuleList(
      ulong assembly,
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      ulong[] modules,
      out uint pNeeded);

    [PreserveSig]
    int GetILForModule(ulong moduleAddr, uint rva, out ulong il);

    // Threads
    [PreserveSig]
    int GetThreadData(ulong thread, out V4ThreadData data);

    [PreserveSig]
    int GetThreadFromThinlockID(uint thinLockId, out ulong pThread);

    [PreserveSig]
    int GetStackLimits(ulong threadPtr, out ulong lower, out ulong upper, out ulong fp);

    // MethodDescs
    [PreserveSig]
    int GetMethodDescData(
      ulong methodDesc,
      ulong ip,
      out V45MethodDescData data,
      uint cRevertedRejitVersions,
      V45ReJitData[] rgRevertedRejitData,
      out ulong pcNeededRevertedRejitData);

    [PreserveSig]
    int GetMethodDescPtrFromIP(ulong ip, out ulong ppMD);

    [PreserveSig]
    int GetMethodDescName(ulong methodDesc, uint count, [Out] StringBuilder name, out uint pNeeded);

    [PreserveSig]
    int GetMethodDescPtrFromFrame(ulong frameAddr, out ulong ppMD);

    [PreserveSig]
    int GetMethodDescFromToken(ulong moduleAddr, uint token, out ulong methodDesc);

    [PreserveSig]
    int GetMethodDescTransparencyData_do_not_use(); //(ulong methodDesc, out DacpMethodDescTransparencyData data);

    // JIT Data
    [PreserveSig]
    int GetCodeHeaderData(ulong ip, out CodeHeaderData data);

    [PreserveSig]
    int GetJitManagerList(
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
      LegacyJitManagerInfo[] jitManagers,
      out uint pNeeded);

    [PreserveSig]
    int GetJitHelperFunctionName(ulong ip, uint count, char name, out uint pNeeded);

    [PreserveSig]
    int GetJumpThunkTarget_do_not_use(uint ctx, out ulong targetIP, out ulong targetMD);

    // ThreadPool
    [PreserveSig]
    int GetThreadpoolData(out V45ThreadPoolData data);

    [PreserveSig]
    int GetWorkRequestData(ulong addrWorkRequest, out V45WorkRequestData data);

    [PreserveSig]
    int GetHillClimbingLogEntry_do_not_use(); //(ulong addr, out DacpHillClimbingLogEntry data);

    // Objects
    [PreserveSig]
    int GetObjectData(ulong objAddr, out V45ObjectData data);

    [PreserveSig]
    int GetObjectStringData(ulong obj, uint count, [Out] StringBuilder stringData, out uint pNeeded);

    [PreserveSig]
    int GetObjectClassName(ulong obj, uint count, [Out] StringBuilder className, out uint pNeeded);

    // MethodTable
    [PreserveSig]
    int GetMethodTableName(ulong mt, uint count, [Out] StringBuilder mtName, out uint pNeeded);

    [PreserveSig]
    int GetMethodTableData(ulong mt, out V45MethodTableData data);

    [PreserveSig]
    int GetMethodTableSlot(ulong mt, uint slot, out ulong value);

    [PreserveSig]
    int GetMethodTableFieldData(ulong mt, out V4FieldInfo data);

    [PreserveSig]
    int GetMethodTableTransparencyData_do_not_use(); //(ulong mt, out DacpMethodTableTransparencyData data);

    // EEClass
    [PreserveSig]
    int GetMethodTableForEEClass(ulong eeClass, out ulong value);

    // FieldDesc
    [PreserveSig]
    int GetFieldDescData(ulong fieldDesc, out LegacyFieldData data);

    // Frames
    [PreserveSig]
    int GetFrameName(ulong vtable, uint count, [Out] StringBuilder frameName, out uint pNeeded);

    // PEFiles
    [PreserveSig]
    int GetPEFileBase(ulong addr, [Out] out ulong baseAddr);

    [PreserveSig]
    int GetPEFileName(ulong addr, uint count, [Out] StringBuilder ptr, [Out] out uint pNeeded);

    // GC
    [PreserveSig]
    int GetGCHeapData(out LegacyGCInfo data);

    [PreserveSig]
    int GetGCHeapList(
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
      ulong[] heaps,
      out uint pNeeded); // svr only

    [PreserveSig]
    int GetGCHeapDetails(ulong heap, out V4HeapDetails details); // wks only

    [PreserveSig]
    int GetGCHeapStaticData(out V4HeapDetails data);

    [PreserveSig]
    int GetHeapSegmentData(ulong seg, out V4SegmentData data);

    [PreserveSig]
    int GetOOMData_do_not_use(); //(ulong oomAddr, out DacpOomData data);

    [PreserveSig]
    int GetOOMStaticData_do_not_use(); //(out DacpOomData data);

    [PreserveSig]
    int GetHeapAnalyzeData_do_not_use(); //(ulong addr, out  DacpGcHeapAnalyzeData data);

    [PreserveSig]
    int GetHeapAnalyzeStaticData_do_not_use(); //(out DacpGcHeapAnalyzeData data);

    // DomainLocal
    [PreserveSig]
    int GetDomainLocalModuleData_do_not_use(); //(ulong addr, out DacpDomainLocalModuleData data);

    [PreserveSig]
    int GetDomainLocalModuleDataFromAppDomain(ulong appDomainAddr, int moduleID, out V45DomainLocalModuleData data);

    [PreserveSig]
    int GetDomainLocalModuleDataFromModule(ulong moduleAddr, out V45DomainLocalModuleData data);

    // ThreadLocal
    [PreserveSig]
    int GetThreadLocalModuleData(ulong thread, uint index, out V45ThreadLocalModuleData data);

    // SyncBlock
    [PreserveSig]
    int GetSyncBlockData(uint number, out LegacySyncBlkData data);

    [PreserveSig]
    int GetSyncBlockCleanupData_do_not_use(); //(ulong addr, out DacpSyncBlockCleanupData data);

    // Handles
    [PreserveSig]
    int GetHandleEnum(
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object ppHandleEnum);

    [PreserveSig]
    int GetHandleEnumForTypes(
      [In] uint[] types,
      uint count,
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object ppHandleEnum);

    [PreserveSig]
    int GetHandleEnumForGC(
      uint gen,
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object ppHandleEnum);

    // EH
    [PreserveSig]
    int TraverseEHInfo_do_not_use(); //(ulong ip, DUMPEHINFO pCallback, IntPtr token);

    [PreserveSig]
    int GetNestedExceptionData(ulong exception, out ulong exceptionObject, out ulong nextNestedException);

    // StressLog
    [PreserveSig]
    int GetStressLogAddress(out ulong stressLog);

    // Heaps
    [PreserveSig]
    int TraverseLoaderHeap(ulong loaderHeapAddr, IntPtr pCallback);

    [PreserveSig]
    int GetCodeHeapList(
      ulong jitManager,
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      LegacyJitCodeHeapInfo[] codeHeaps,
      out uint pNeeded);

    [PreserveSig]
    int TraverseVirtCallStubHeap(ulong pAppDomain, uint heaptype, IntPtr pCallback);

    // Other
    [PreserveSig]
    int GetUsefulGlobals(out CommonMethodTables data);

    [PreserveSig]
    int GetClrWatsonBuckets(ulong thread, out IntPtr pGenericModeBlock);

    [PreserveSig]
    int GetTLSIndex(out uint pIndex);

    [PreserveSig]
    int GetDacModuleHandle(out IntPtr phModule);

    // COM
    [PreserveSig]
    int GetRCWData(ulong addr, out V45RCWData data);

    [PreserveSig]
    int GetRCWInterfaces(
      ulong rcw,
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      COMInterfacePointerData[] interfaces,
      out uint pNeeded);

    [PreserveSig]
    int GetCCWData(ulong ccw, out V45CCWData data);

    [PreserveSig]
    int GetCCWInterfaces(
      ulong ccw,
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      COMInterfacePointerData[] interfaces,
      out uint pNeeded);

    [PreserveSig]
    int TraverseRCWCleanupList_do_not_use(); //(ulong cleanupListPtr, VISITRCWFORCLEANUP pCallback, LPVOID token);

    // GC Reference Functions
    [PreserveSig]
    int GetStackReferences(
      uint osThreadID,
      [Out][MarshalAs(UnmanagedType.IUnknown)]
      out object ppEnum);

    [PreserveSig]
    int GetRegisterName(int regName, uint count, [Out] StringBuilder buffer, out uint pNeeded);

    [PreserveSig]
    int GetThreadAllocData(ulong thread, ref V45AllocData data);

    [PreserveSig]
    int GetHeapAllocData(
      uint count,
      [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
      V45GenerationAllocData[] data,
      out uint pNeeded);

    // For BindingDisplay plugin
    [PreserveSig]
    int GetFailedAssemblyList(ulong appDomain, int count, ulong[] values, out uint pNeeded);

    [PreserveSig]
    int GetPrivateBinPaths(ulong appDomain, int count, [Out] StringBuilder paths, out uint pNeeded);

    [PreserveSig]
    int GetAssemblyLocation(ulong assembly, int count, [Out] StringBuilder location, out uint pNeeded);

    [PreserveSig]
    int GetAppDomainConfigFile(ulong appDomain, int count, [Out] StringBuilder configFile, out uint pNeeded);

    [PreserveSig]
    int GetApplicationBase(ulong appDomain, int count, [Out] StringBuilder appBase, out uint pNeeded);

    [PreserveSig]
    int GetFailedAssemblyData(ulong assembly, out uint pContext, out int pResult);

    [PreserveSig]
    int GetFailedAssemblyLocation(ulong assesmbly, uint count, [Out] StringBuilder location, out uint pNeeded);

    [PreserveSig]
    int GetFailedAssemblyDisplayName(ulong assembly, uint count, [Out] StringBuilder name, out uint pNeeded);
  }
}