// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.ICorDebug;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal abstract class DesktopBaseModule : ClrModule
  {
    protected DesktopRuntimeBase _runtime;

    public override ClrRuntime Runtime => _runtime;

    internal abstract ulong GetDomainModule(ClrAppDomain appDomain);

    internal ulong ModuleId { get; set; }

    internal virtual IMetadataImport GetMetadataImport()
    {
      return null;
    }

    public int Revision { get; set; }

    public DesktopBaseModule(DesktopRuntimeBase runtime)
    {
      _runtime = runtime;
    }
  }

  internal class DesktopModule : DesktopBaseModule
  {
    private static readonly PdbInfo s_failurePdb = new PdbInfo();

    private readonly bool _reflection;
    private readonly bool _isPE;
    private readonly string _name;
    private readonly string _assemblyName;
    private IMetadataImport _metadata;
    private readonly Dictionary<ClrAppDomain, ulong> _mapping = new Dictionary<ClrAppDomain, ulong>();
    private readonly ulong _address;
    private readonly ulong _imageBase;
    private readonly Lazy<ulong> _size;
    private readonly ulong _metadataStart;
    private readonly ulong _metadataLength;
    private DebuggableAttribute.DebuggingModes? _debugMode;
    private readonly ulong _assemblyAddress;
    private bool _typesLoaded;
    private ClrAppDomain[] _appDomainList;
    private PdbInfo _pdb;

    public DesktopModule(DesktopRuntimeBase runtime, ulong address, IModuleData data, string name, string assemblyName)
      : base(runtime)
    {
      _address = address;
      Revision = runtime.Revision;
      _imageBase = data.ImageBase;
      _assemblyName = assemblyName;
      _isPE = data.IsPEFile;
      _reflection = data.IsReflection || string.IsNullOrEmpty(name);
      _name = name;
      ModuleId = data.ModuleId;
      ModuleIndex = data.ModuleIndex;
      _metadataStart = data.MetdataStart;
      _metadataLength = data.MetadataLength;
      _assemblyAddress = data.Assembly;
      _size = new Lazy<ulong>(() => runtime.GetModuleSize(address));

      if (!runtime.DataReader.IsMinidump) _metadata = data.LegacyMetaDataImport as IMetadataImport;
    }

    internal override ulong Address => _address;

    public override PdbInfo Pdb
    {
      get
      {
        if (_pdb == null)
          try
          {
            using (var pefile = new PEFile(new ReadVirtualStream(_runtime.DataReader, (long)ImageBase, (long)(Size > 0 ? Size : 0x1000)), true))
            {
              _pdb = pefile.PdbInfo ?? s_failurePdb;
            }
          }
          catch
          {
          }

        return _pdb != s_failurePdb ? _pdb : null;
      }
    }

    internal ulong GetMTForDomain(ClrAppDomain domain, DesktopHeapType type)
    {
      DesktopGCHeap heap = null;
      var mtList = _runtime.GetMethodTableList(_mapping[domain]);

      var hasToken = type.MetadataToken != 0 && type.MetadataToken != uint.MaxValue;

      var token = ~0xff000000 & type.MetadataToken;

      foreach (var pair in mtList)
        if (hasToken)
        {
          if (pair.Token == token)
            return pair.MethodTable;
        }
        else
        {
          if (heap == null)
            heap = (DesktopGCHeap)_runtime.Heap;

          if (heap.GetTypeByMethodTable(pair.MethodTable, 0) == type)
            return pair.MethodTable;
        }

      return 0;
    }

    public override IEnumerable<ClrType> EnumerateTypes()
    {
      var heap = (DesktopGCHeap)_runtime.Heap;
      var mtList = _runtime.GetMethodTableList(_address);
      if (_typesLoaded)
      {
        foreach (var type in heap.EnumerateTypes())
          if (type.Module == this)
            yield return type;
      }
      else
      {
        if (mtList != null)
          foreach (var pair in mtList)
          {
            var mt = pair.MethodTable;
            if (mt != _runtime.ArrayMethodTable)
            {
              // prefetch element type, as this also can load types
              var type = heap.GetTypeByMethodTable(mt, 0, 0);
              if (type != null)
                yield return type;
            }
          }

        _typesLoaded = true;
      }
    }

    public override string AssemblyName => _assemblyName;

    public override string Name => _name;

    public override bool IsDynamic => _reflection;

    public override bool IsFile => _isPE;

    public override string FileName => _isPE ? _name : null;

    internal ulong ModuleIndex { get; }

    internal void AddMapping(ClrAppDomain domain, ulong domainModule)
    {
      var appDomain = (DesktopAppDomain)domain;
      _mapping[domain] = domainModule;
    }

    public override IList<ClrAppDomain> AppDomains
    {
      get
      {
        if (_appDomainList == null)
        {
          _appDomainList = new ClrAppDomain[_mapping.Keys.Count];
          _appDomainList = _mapping.Keys.ToArray();
          Array.Sort(_appDomainList, (d, d2) => d.Id.CompareTo(d2.Id));
        }

        return _appDomainList;
      }
    }

    internal override ulong GetDomainModule(ClrAppDomain domain)
    {
      var domains = _runtime.AppDomains;
      if (domain == null)
      {
        foreach (var addr in _mapping.Values)
          return addr;

        return 0;
      }

      if (_mapping.TryGetValue(domain, out var value))
        return value;

      return 0;
    }

    internal override IMetadataImport GetMetadataImport()
    {
      if (Revision != _runtime.Revision)
        ClrDiagnosticsException.ThrowRevisionError(Revision, _runtime.Revision);

      if (_metadata != null)
        return _metadata;

      _metadata = _runtime.GetMetadataImport(_address);
      return _metadata;
    }

    public override ulong ImageBase => _imageBase;

    public override ulong Size => _size.Value;

    public override ulong MetadataAddress => _metadataStart;

    public override ulong MetadataLength => _metadataLength;

    public override object MetadataImport => GetMetadataImport();

    public override DebuggableAttribute.DebuggingModes DebuggingMode
    {
      get
      {
        if (_debugMode == null)
          InitDebugAttributes();

        Debug.Assert(_debugMode != null);
        return _debugMode.Value;
      }
    }

    private void InitDebugAttributes()
    {
      var metadata = GetMetadataImport();
      if (metadata == null)
      {
        _debugMode = DebuggableAttribute.DebuggingModes.None;
        return;
      }

      try
      {
        var hr = metadata.GetCustomAttributeByName(0x20000001, "System.Diagnostics.DebuggableAttribute", out var data, out var cbData);
        if (hr != 0 || cbData <= 4)
        {
          _debugMode = DebuggableAttribute.DebuggingModes.None;
          return;
        }

        unsafe
        {
          var b = (byte*)data.ToPointer();
          ushort opt = b[2];
          ushort dbg = b[3];

          _debugMode = (DebuggableAttribute.DebuggingModes)((dbg << 8) | opt);
        }
      }
      catch (SEHException)
      {
        _debugMode = DebuggableAttribute.DebuggingModes.None;
      }
    }

    public override ClrType GetTypeByName(string name)
    {
      foreach (var type in EnumerateTypes())
        if (type.Name == name)
          return type;

      return null;
    }

    public override ulong AssemblyId => _assemblyAddress;
  }

  internal class ErrorModule : DesktopBaseModule
  {
    private static uint s_id;
    private readonly uint _id = s_id++;

    public ErrorModule(DesktopRuntimeBase runtime)
      : base(runtime)
    {
    }

    public override PdbInfo Pdb => null;

    public override IList<ClrAppDomain> AppDomains => new ClrAppDomain[0];

    public override string AssemblyName => "<error>";

    public override string Name => "<error>";

    public override bool IsDynamic => false;

    public override bool IsFile => false;

    public override string FileName => "<error>";

    public override ulong ImageBase => 0;

    public override ulong Size => 0;

    public override IEnumerable<ClrType> EnumerateTypes()
    {
      return new ClrType[0];
    }

    public override ulong MetadataAddress => 0;

    public override ulong MetadataLength => 0;

    public override object MetadataImport => null;

    internal override ulong GetDomainModule(ClrAppDomain appDomain)
    {
      return 0;
    }

    public override DebuggableAttribute.DebuggingModes DebuggingMode => DebuggableAttribute.DebuggingModes.None;

    public override ClrType GetTypeByName(string name)
    {
      return null;
    }

    public override ulong AssemblyId => _id;
  }
}