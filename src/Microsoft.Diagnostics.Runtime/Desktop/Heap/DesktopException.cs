﻿using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopException : ClrException
  {
    public DesktopException(ulong objRef, BaseDesktopHeapType type)
    {
      _object = objRef;
      _type = type;
    }

    public override ClrType Type => _type;

    public override string Message
    {
      get
      {
        var field = _type.GetFieldByName("_message");
        if (field != null)
          return (string)field.GetValue(_object);

        var runtime = _type.DesktopHeap.DesktopRuntime;
        var offset = runtime.GetExceptionMessageOffset();
        Debug.Assert(offset > 0);

        var message = _object + offset;
        if (!runtime.ReadPointer(message, out message))
          return null;

        return _type.DesktopHeap.GetStringContents(message);
      }
    }

    public override ulong Address => _object;

    public override ClrException Inner
    {
      get
      {
        // TODO:  This needs to get the field offset by runtime instead.
        var field = _type.GetFieldByName("_innerException");
        if (field == null)
          return null;

        var inner = field.GetValue(_object);
        if (inner == null || !(inner is ulong) || (ulong)inner == 0)
          return null;

        var ex = (ulong)inner;
        var type = (BaseDesktopHeapType)_type.DesktopHeap.GetObjectType(ex);

        return new DesktopException(ex, type);
      }
    }

    public override IList<ClrStackFrame> StackTrace
    {
      get
      {
        if (_stackTrace == null)
          _stackTrace = _type.DesktopHeap.DesktopRuntime.GetExceptionStackTrace(_object, _type);

        return _stackTrace;
      }
    }

    public override int HResult
    {
      get
      {
        var field = _type.GetFieldByName("_HResult");
        if (field != null)
          return (int)field.GetValue(_object);

        var runtime = _type.DesktopHeap.DesktopRuntime;
        var offset = runtime.GetExceptionHROffset();
        runtime.ReadDword(_object + offset, out int hr);

        return hr;
      }
    }

    private readonly ulong _object;
    private readonly BaseDesktopHeapType _type;
    private IList<ClrStackFrame> _stackTrace;
  }
}