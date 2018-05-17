﻿using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class LegacyGCHeap : DesktopGCHeap
  {
    private ClrObject _lastObject;
    private readonly Dictionary<TypeHandle, int> _indices = new Dictionary<TypeHandle, int>(TypeHandle.EqualityComparer);

    public LegacyGCHeap(DesktopRuntimeBase runtime)
      : base(runtime)
    {
    }

    public override bool HasComponentMethodTables => true;

    public override ClrType GetTypeByMethodTable(ulong mt, ulong cmt)
    {
      return GetTypeByMethodTable(mt, cmt, 0);
    }

    internal override ClrType GetTypeByMethodTable(ulong mt, ulong cmt, ulong obj)
    {
      if (mt == 0)
        return null;

      ClrType componentType = null;
      if (mt == DesktopRuntime.ArrayMethodTable)
      {
        if (cmt != 0)
        {
          componentType = GetTypeByMethodTable(cmt, 0);
          if (componentType != null)
          {
            cmt = componentType.MethodTable;
          }
          else if (obj != 0)
          {
            componentType = TryGetComponentType(obj, cmt);
            if (componentType != null)
              cmt = componentType.MethodTable;
          }
        }
        else
        {
          componentType = ObjectType;
          cmt = ObjectType.MethodTable;
        }
      }
      else
      {
        cmt = 0;
      }

      var hnd = new TypeHandle(mt, cmt);
      ClrType ret = null;

      // See if we already have the type.
      if (_indices.TryGetValue(hnd, out var index))
      {
        ret = _types[index];
      }
      else if (mt == DesktopRuntime.ArrayMethodTable && cmt == 0)
      {
        // Handle the case where the methodtable is an array, but the component method table
        // was not specified.  (This happens with fields.)  In this case, return System.Object[],
        // with an ArrayComponentType set to System.Object.
        var token = DesktopRuntime.GetMetadataToken(mt);
        if (token == 0xffffffff)
          return null;

        var modEnt = new ModuleEntry(ArrayType.Module, token);

        ret = ArrayType;
        index = _types.Count;

        _indices[hnd] = index;
        _typeEntry[modEnt] = index;
        _types.Add(ret);

        Debug.Assert(_types[index] == ret);
      }
      else
      {
        // No, so we'll have to construct it.
        var moduleAddr = DesktopRuntime.GetModuleForMT(hnd.MethodTable);
        var module = DesktopRuntime.GetModule(moduleAddr);
        var token = DesktopRuntime.GetMetadataToken(mt);

        var isFree = mt == DesktopRuntime.FreeMethodTable;
        if (token == 0xffffffff && !isFree)
          return null;

        // Dynamic functions/modules
        var tokenEnt = token;
        if (!isFree && (module == null || module.IsDynamic))
          tokenEnt = (uint)mt;

        var modEnt = new ModuleEntry(module, tokenEnt);

        if (ret == null)
        {
          var mtData = DesktopRuntime.GetMethodTableData(mt);
          if (mtData == null)
            return null;

          ret = new DesktopHeapType(() => GetTypeName(hnd, module, token), module, token, mt, mtData, this) {ComponentType = componentType};
          index = _types.Count;
          ((DesktopHeapType)ret).SetIndex(index);
          _indices[hnd] = index;
          _typeEntry[modEnt] = index;
          _types.Add(ret);

          Debug.Assert(_types[index] == ret);
        }
      }

      if (obj != 0 && ret.ComponentType == null && ret.IsArray)
        ret.ComponentType = TryGetComponentType(obj, cmt);

      return ret;
    }

    public override ClrType GetObjectType(ulong objRef)
    {
      ulong mt, cmt = 0;

      if (_lastObject.Address == objRef)
        return _lastObject.Type;

      if (IsHeapCached)
        return base.GetObjectType(objRef);

      var cache = MemoryReader;
      if (cache.Contains(objRef))
      {
        if (!cache.ReadPtr(objRef, out mt))
          return null;
      }
      else if (DesktopRuntime.MemoryReader.Contains(objRef))
      {
        cache = DesktopRuntime.MemoryReader;
        if (!cache.ReadPtr(objRef, out mt))
          return null;
      }
      else
      {
        cache = null;
        mt = DesktopRuntime.DataReader.ReadPointerUnsafe(objRef);
      }

      if (((int)mt & 3) != 0)
        mt &= ~3UL;

      if (mt == DesktopRuntime.ArrayMethodTable)
      {
        var elemenTypeOffset = (uint)PointerSize * 2;
        if (cache == null)
          cmt = DesktopRuntime.DataReader.ReadPointerUnsafe(objRef + elemenTypeOffset);
        else if (!cache.ReadPtr(objRef + elemenTypeOffset, out cmt))
          return null;
      }
      else
      {
        cmt = 0;
      }

      var type = GetTypeByMethodTable(mt, cmt, objRef);
      _lastObject = ClrObject.Create(objRef, type);

      return type;
    }
  }
}