﻿using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   Represents a Clr handle in the target process.
  /// </summary>
  public class ClrHandle
  {
    /// <summary>
    ///   The address of the handle itself.  That is, *ulong == Object.
    /// </summary>
    public ulong Address { get; set; }

    /// <summary>
    ///   The Object the handle roots.
    /// </summary>
    public ulong Object { get; set; }

    /// <summary>
    ///   The the type of the Object.
    /// </summary>
    public ClrType Type { get; set; }

    /// <summary>
    ///   Whether the handle is strong (roots the object) or not.
    /// </summary>
    public virtual bool IsStrong
    {
      get
      {
        switch (HandleType)
        {
          case HandleType.RefCount:
            return RefCount > 0;

          case HandleType.WeakLong:
          case HandleType.WeakShort:
          case HandleType.Dependent:
            return false;

          default:
            return true;
        }
      }
    }

    /// <summary>
    ///   Whether or not the handle pins the object (doesn't allow the GC to
    ///   relocate it) or not.
    /// </summary>
    public virtual bool IsPinned => HandleType == HandleType.AsyncPinned || HandleType == HandleType.Pinned;

    /// <summary>
    ///   Gets the type of handle.
    /// </summary>
    public HandleType HandleType { get; set; }

    /// <summary>
    ///   If this handle is a RefCount handle, this returns the reference count.
    ///   RefCount handles with a RefCount > 0 are strong.
    ///   NOTE: v2 CLR CANNOT determine the RefCount.  We always set the RefCount
    ///   to 1 in a v2 query since a strong RefCount handle is the common case.
    /// </summary>
    public uint RefCount { get; set; }

    /// <summary>
    ///   Set only if the handle type is a DependentHandle.  Dependent handles add
    ///   an extra edge to the object graph.  Meaning, this.Object now roots the
    ///   dependent target, but only if this.Object is alive itself.
    ///   NOTE: CLRs prior to v4.5 cannot obtain the dependent target.  This field will
    ///   be 0 for any CLR prior to v4.5.
    /// </summary>
    public ulong DependentTarget { get; set; }

    /// <summary>
    ///   The type of the dependent target, if non 0.
    /// </summary>
    public ClrType DependentType { get; set; }

    /// <summary>
    ///   The AppDomain the handle resides in.
    /// </summary>
    public ClrAppDomain AppDomain { get; set; }

    /// <summary>
    ///   ToString override.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return HandleType + " " + (Type != null ? Type.Name : "");
    }

    internal ClrHandle()
    {
    }

    internal ClrHandle(V45Runtime clr, ClrHeap heap, HandleData handleData)
    {
      Address = handleData.Handle;
      clr.ReadPointer(Address, out var obj);

      Object = obj;
      Type = heap.GetObjectType(obj);

      uint refCount = 0;

      if (handleData.Type == (int)HandleType.RefCount)
      {
        if (handleData.IsPegged != 0)
          refCount = handleData.JupiterRefCount;

        if (refCount < handleData.RefCount)
          refCount = handleData.RefCount;

        if (Type != null)
        {
          if (Type.IsCCW(obj))
          {
            var data = Type.GetCCWData(obj);
            if (data != null && refCount < data.RefCount)
              refCount = (uint)data.RefCount;
          }
          else if (Type.IsRCW(obj))
          {
            var data = Type.GetRCWData(obj);
            if (data != null && refCount < data.RefCount)
              refCount = (uint)data.RefCount;
          }
        }

        RefCount = refCount;
      }

      HandleType = (HandleType)handleData.Type;
      AppDomain = clr.GetAppDomainByAddress(handleData.AppDomain);

      if (HandleType == HandleType.Dependent)
      {
        DependentTarget = handleData.Secondary;
        DependentType = heap.GetObjectType(handleData.Secondary);
      }
    }

    internal ClrHandle GetInteriorHandle()
    {
      if (HandleType != HandleType.AsyncPinned)
        return null;

      if (Type == null)
        return null;

      var field = Type.GetFieldByName("m_userObject");
      if (field == null)
        return null;

      ulong obj;
      var tmp = field.GetValue(Object);
      if (!(tmp is ulong) || (obj = (ulong)tmp) == 0)
        return null;

      var type = Type.Heap.GetObjectType(obj);
      if (type == null)
        return null;

      var result = new ClrHandle
      {
        Object = obj,
        Type = type,
        Address = Address,
        AppDomain = AppDomain,
        HandleType = HandleType
      };
      return result;
    }
  }
}