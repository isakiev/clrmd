using System;

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45ModuleData : IModuleData
  {
    public ulong address;
    public ulong peFile;
    public ulong ilBase;
    public ulong metadataStart;
    public ulong metadataSize;
    public ulong assembly;
    public uint bIsReflection;
    public uint bIsPEFile;
    public ulong dwBaseClassIndex;
    public ulong dwModuleID;
    public uint dwTransientFlags;
    public ulong TypeDefToMethodTableMap;
    public ulong TypeRefToMethodTableMap;
    public ulong MethodDefToDescMap;
    public ulong FieldDefToDescMap;
    public ulong MemberRefToDescMap;
    public ulong FileReferencesMap;
    public ulong ManifestModuleReferencesMap;
    public ulong pLookupTableHeap;
    public ulong pThunkHeap;
    public ulong dwModuleIndex;

    public ulong Assembly => assembly;

    public ulong PEFile => bIsPEFile == 0 ? ilBase : peFile;
    public ulong LookupTableHeap => pLookupTableHeap;
    public ulong ThunkHeap => pThunkHeap;

    public IntPtr LegacyMetaDataImport => IntPtr.Zero;

    public ulong ModuleId => dwModuleID;

    public ulong ModuleIndex => dwModuleIndex;

    public bool IsReflection => bIsReflection != 0;

    public bool IsPEFile => bIsPEFile != 0;
    public ulong ImageBase => ilBase;
    public ulong MetdataStart => metadataStart;

    public ulong MetadataLength => metadataSize;
  }
}