﻿using System;
using System.Runtime.InteropServices;

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V4ModuleData : IModuleData
  {
    public ulong peFile;
    public ulong ilBase;
    public ulong metadataStart;
    public IntPtr metadataSize;
    public ulong assembly;
    public uint bIsReflection;
    public uint bIsPEFile;
    public IntPtr dwBaseClassIndex;
    [MarshalAs(UnmanagedType.IUnknown)]
    public object ModuleDefinition;
    public IntPtr dwModuleID;

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

    public IntPtr dwModuleIndex;

    public ulong PEFile => peFile;

    public ulong Assembly => assembly;

    public ulong ImageBase => ilBase;

    public ulong LookupTableHeap => pLookupTableHeap;

    public ulong ThunkHeap => pThunkHeap;

    public object LegacyMetaDataImport => ModuleDefinition;

    public ulong ModuleId => (ulong)dwModuleID.ToInt64();

    public ulong ModuleIndex => (ulong)dwModuleIndex.ToInt64();

    public bool IsReflection => bIsReflection != 0;

    public bool IsPEFile => bIsPEFile != 0;

    public ulong MetdataStart => metadataStart;

    public ulong MetadataLength => (ulong)metadataSize.ToInt64();
  }
}