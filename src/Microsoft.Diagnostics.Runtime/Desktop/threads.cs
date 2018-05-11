// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopStackFrame : ClrStackFrame
  {
    public override ClrThread Thread => _thread;

    public override ulong StackPointer => _sp;

    public override ulong InstructionPointer => _ip;

    public override ClrStackFrameType Kind => _type;

    public override string DisplayString => _frameName;

    public override ClrMethod Method
    {
      get
      {
        if (_method == null && _ip != 0 && _type == ClrStackFrameType.ManagedMethod)
          _method = _runtime.GetMethodByAddress(_ip);

        return _method;
      }
    }

    public ICorDebugILFrame CordbFrame { get; internal set; }

    public override string ToString()
    {
      if (_type == ClrStackFrameType.ManagedMethod)
        return _frameName;

      var methodLen = 0;
      var methodTypeLen = 0;

      if (_method != null)
      {
        methodLen = _method.Name.Length;
        if (_method.Type != null)
          methodTypeLen = _method.Type.Name.Length;
      }

      var sb = new StringBuilder(_frameName.Length + methodLen + methodTypeLen + 10);

      sb.Append('[');
      sb.Append(_frameName);
      sb.Append(']');

      if (_method != null)
      {
        sb.Append(" (");

        if (_method.Type != null)
        {
          sb.Append(_method.Type.Name);
          sb.Append('.');
        }

        sb.Append(_method.Name);
        sb.Append(')');
      }

      return sb.ToString();
    }

    public DesktopStackFrame(DesktopRuntimeBase runtime, DesktopThread thread, ulong ip, ulong sp, ulong md)
    {
      _runtime = runtime;
      _thread = thread;
      _ip = ip;
      _sp = sp;
      _frameName = _runtime.GetNameForMD(md) ?? "Unknown";
      _type = ClrStackFrameType.ManagedMethod;

      InitMethod(md);
    }

    public DesktopStackFrame(DesktopRuntimeBase runtime, DesktopThread thread, ulong sp, ulong md)
    {
      _runtime = runtime;
      _thread = thread;
      _sp = sp;
      _frameName = _runtime.GetNameForMD(md) ?? "Unknown";
      _type = ClrStackFrameType.Runtime;

      InitMethod(md);
    }

    public DesktopStackFrame(DesktopRuntimeBase runtime, DesktopThread thread, ulong sp, string method, ClrMethod innerMethod)
    {
      _runtime = runtime;
      _thread = thread;
      _sp = sp;
      _frameName = method ?? "Unknown";
      _type = ClrStackFrameType.Runtime;
      _method = innerMethod;
    }

    private void InitMethod(ulong md)
    {
      if (_method != null)
        return;

      if (_ip != 0 && _type == ClrStackFrameType.ManagedMethod)
      {
        _method = _runtime.GetMethodByAddress(_ip);
      }
      else if (md != 0)
      {
        var mdData = _runtime.GetMethodDescData(md);
        _method = DesktopMethod.Create(_runtime, mdData);
      }
    }

    private readonly ulong _ip;
    private readonly ulong _sp;
    private readonly string _frameName;
    private readonly ClrStackFrameType _type;
    private ClrMethod _method;
    private readonly DesktopRuntimeBase _runtime;
    private readonly DesktopThread _thread;
  }

  internal class DesktopThread : ThreadBase
  {
    internal DesktopRuntimeBase DesktopRuntime => _runtime;

    internal ICorDebugThread CorDebugThread => DesktopRuntime.GetCorDebugThread(OSThreadId);

    public override ClrRuntime Runtime => _runtime;

    public override ClrException CurrentException
    {
      get
      {
        var ex = _exception;
        if (ex == 0)
          return null;

        if (!_runtime.ReadPointer(ex, out ex) || ex == 0)
          return null;

        return _runtime.Heap.GetExceptionObject(ex);
      }
    }

    public override ulong StackBase
    {
      get
      {
        if (_teb == 0)
          return 0;

        var ptr = _teb + (ulong)IntPtr.Size;
        if (!_runtime.ReadPointer(ptr, out ptr))
          return 0;

        return ptr;
      }
    }

    public override ulong StackLimit
    {
      get
      {
        if (_teb == 0)
          return 0;

        var ptr = _teb + (ulong)IntPtr.Size * 2;
        if (!_runtime.ReadPointer(ptr, out ptr))
          return 0;

        return ptr;
      }
    }

    public override IEnumerable<ClrRoot> EnumerateStackObjects()
    {
      return _runtime.EnumerateStackReferences(this, true);
    }

    public override IEnumerable<ClrRoot> EnumerateStackObjects(bool includePossiblyDead)
    {
      return _runtime.EnumerateStackReferences(this, includePossiblyDead);
    }

    public override IList<ClrStackFrame> StackTrace
    {
      get
      {
        if (_stackTrace == null)
        {
          var frames = new List<ClrStackFrame>(32);

          var lastSP = ulong.MaxValue;
          var spCount = 0;

          var max = 4096;
          foreach (var frame in _runtime.EnumerateStackFrames(this))
          {
            // We only allow a maximum of 4096 frames to be enumerated out of this stack trace to
            // ensure we don't hit degenerate cases of stack unwind where we never make progress
            // but the stack pointer keeps changing somehow.
            if (max-- == 0)
              break;

            if (frame.StackPointer == lastSP)
            {
              // If we hit five stack frames with the same stack pointer then we aren't making progress
              // in the unwind.  At that point we need to stop to ensure we don't loop infinitely.
              if (spCount++ >= 5)
                break;
            }
            else
            {
              lastSP = frame.StackPointer;
              spCount = 0;
            }

            frames.Add(frame);
          }

          _stackTrace = frames.ToArray();
        }

        return _stackTrace;
      }
    }

    internal unsafe void InitLocalData()
    {
      if (_corDebugInit)
        return;

      _corDebugInit = true;

      var thread = (ICorDebugThread3)CorDebugThread;
      thread.CreateStackWalk(out var stackwalk);

      do
      {
        stackwalk.GetFrame(out var frame);

        var ilFrame = frame as ICorDebugILFrame;
        if (ilFrame == null)
          continue;

        var context = ContextHelper.Context;

        fixed (byte* ptr = context)
        {
          stackwalk.GetContext(ContextHelper.ContextFlags, ContextHelper.Length, out var size, new IntPtr(ptr));
        }

        ulong ip = BitConverter.ToUInt32(context, ContextHelper.InstructionPointerOffset);
        ulong sp = BitConverter.ToUInt32(context, ContextHelper.StackPointerOffset);

        var result = _stackTrace.Where(frm => sp == frm.StackPointer && ip == frm.InstructionPointer).Select(p => (DesktopStackFrame)p).SingleOrDefault();
        if (result != null)
          result.CordbFrame = ilFrame;
      } while (stackwalk.Next() == 0);
    }

    public override IEnumerable<ClrStackFrame> EnumerateStackTrace()
    {
      return _runtime.EnumerateStackFrames(this);
    }

    public override IList<BlockingObject> BlockingObjects
    {
      get
      {
        ((DesktopGCHeap)_runtime.Heap).InitLockInspection();

        if (_blockingObjs == null)
          return new BlockingObject[0];

        return _blockingObjs;
      }
    }

    internal DesktopThread(DesktopRuntimeBase clr, IThreadData thread, ulong address, bool finalizer)
      : base(thread, address, finalizer)
    {
      _runtime = clr;
    }

    private readonly DesktopRuntimeBase _runtime;
    private bool _corDebugInit;
  }

  internal class LocalVarRoot : ClrRoot
  {
    private readonly bool _pinned;
    private readonly bool _falsePos;
    private readonly bool _interior;
    private readonly ClrThread _thread;
    private readonly ClrType _type;
    private readonly ClrAppDomain _domain;
    private readonly ClrStackFrame _stackFrame;

    public LocalVarRoot(ulong addr, ulong obj, ClrType type, ClrAppDomain domain, ClrThread thread, bool pinned, bool falsePos, bool interior, ClrStackFrame stackFrame)
    {
      Address = addr;
      Object = obj;
      _pinned = pinned;
      _falsePos = falsePos;
      _interior = interior;
      _domain = domain;
      _thread = thread;
      _type = type;
      _stackFrame = stackFrame;
    }

    public override ClrStackFrame StackFrame => _stackFrame;

    public override ClrAppDomain AppDomain => _domain;

    public override ClrThread Thread => _thread;

    public override bool IsPossibleFalsePositive => _falsePos;

    public override string Name => "local var";

    public override bool IsPinned => _pinned;

    public override GCRootKind Kind => GCRootKind.LocalVar;

    public override bool IsInterior => _interior;

    public override ClrType Type => _type;
  }
}