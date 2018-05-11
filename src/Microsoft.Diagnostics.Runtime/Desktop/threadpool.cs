// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopThreadPool : ClrThreadPool
  {
    private readonly DesktopRuntimeBase _runtime;
    private ClrHeap _heap;
    private readonly int _totalThreads;
    private readonly int _runningThreads;
    private readonly int _idleThreads;
    private readonly int _minThreads;
    private readonly int _maxThreads;
    private readonly int _minCP;
    private readonly int _maxCP;
    private readonly int _cpu;
    private readonly int _freeCP;
    private readonly int _maxFreeCP;

    public DesktopThreadPool(DesktopRuntimeBase runtime, IThreadPoolData data)
    {
      _runtime = runtime;
      _totalThreads = data.TotalThreads;
      _runningThreads = data.RunningThreads;
      _idleThreads = data.IdleThreads;
      _minThreads = data.MinThreads;
      _maxThreads = data.MaxThreads;
      _minCP = data.MinCP;
      _maxCP = data.MaxCP;
      _cpu = data.CPU;
      _freeCP = data.NumFreeCP;
      _maxFreeCP = data.MaxFreeCP;
    }

    public override int TotalThreads => _totalThreads;

    public override int RunningThreads => _runningThreads;

    public override int IdleThreads => _idleThreads;

    public override int MinThreads => _minThreads;

    public override int MaxThreads => _maxThreads;

    public override IEnumerable<NativeWorkItem> EnumerateNativeWorkItems()
    {
      return _runtime.EnumerateWorkItems();
    }

    public override IEnumerable<ManagedWorkItem> EnumerateManagedWorkItems()
    {
      foreach (var obj in EnumerateManagedThreadpoolObjects())
        if (obj != 0)
        {
          var type = _heap.GetObjectType(obj);
          if (type != null)
            yield return new DesktopManagedWorkItem(type, obj);
        }
    }

    private IEnumerable<ulong> EnumerateManagedThreadpoolObjects()
    {
      _heap = _runtime.Heap;

      var mscorlib = GetMscorlib();
      if (mscorlib != null)
      {
        var queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolGlobals");
        if (queueType != null)
        {
          var workQueueField = queueType.GetStaticFieldByName("workQueue");
          if (workQueueField != null)
            foreach (var appDomain in _runtime.AppDomains)
            {
              var workQueueValue = workQueueField.GetValue(appDomain);
              var workQueue = workQueueValue == null ? 0L : (ulong)workQueueValue;
              var workQueueType = _heap.GetObjectType(workQueue);

              if (workQueue == 0 || workQueueType == null)
                continue;

              ulong queueHead;
              do
              {
                if (!GetFieldObject(workQueueType, workQueue, "queueHead", out var queueHeadType, out queueHead))
                  break;

                if (GetFieldObject(queueHeadType, queueHead, "nodes", out var nodesType, out var nodes) && nodesType.IsArray)
                {
                  var len = nodesType.GetArrayLength(nodes);
                  for (var i = 0; i < len; ++i)
                  {
                    var addr = (ulong)nodesType.GetArrayElementValue(nodes, i);
                    if (addr != 0)
                      yield return addr;
                  }
                }

                if (!GetFieldObject(queueHeadType, queueHead, "Next", out queueHeadType, out queueHead))
                  break;
              } while (queueHead != 0);
            }
        }

        queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolWorkQueue");
        if (queueType != null)
        {
          var threadQueuesField = queueType.GetStaticFieldByName("allThreadQueues");
          if (threadQueuesField != null)
            foreach (var domain in _runtime.AppDomains)
            {
              var threadQueue = (ulong?)threadQueuesField.GetValue(domain);
              if (!threadQueue.HasValue || threadQueue.Value == 0)
                continue;

              var threadQueueType = _heap.GetObjectType(threadQueue.Value);
              if (threadQueueType == null)
                continue;

              if (!GetFieldObject(threadQueueType, threadQueue.Value, "m_array", out var outerArrayType, out var outerArray) || !outerArrayType.IsArray)
                continue;

              var outerLen = outerArrayType.GetArrayLength(outerArray);
              for (var i = 0; i < outerLen; ++i)
              {
                var entry = (ulong)outerArrayType.GetArrayElementValue(outerArray, i);
                if (entry == 0)
                  continue;

                var entryType = _heap.GetObjectType(entry);
                if (entryType == null)
                  continue;

                if (!GetFieldObject(entryType, entry, "m_array", out var arrayType, out var array) || !arrayType.IsArray)
                  continue;

                var len = arrayType.GetArrayLength(array);
                for (var j = 0; j < len; ++j)
                {
                  var addr = (ulong)arrayType.GetArrayElementValue(array, i);
                  if (addr != 0)
                    yield return addr;
                }
              }
            }
        }
      }
    }

    private ClrModule GetMscorlib()
    {
      foreach (var module in _runtime.Modules)
        if (module.AssemblyName.Contains("mscorlib.dll"))
          return module;

      // Uh oh, this shouldn't have happened.  Let's look more carefully (slowly).
      foreach (var module in _runtime.Modules)
        if (module.AssemblyName.ToLower().Contains("mscorlib"))
          return module;

      // Ok...not sure why we couldn't find it.
      return null;
    }

    private bool GetFieldObject(ClrType type, ulong obj, string fieldName, out ClrType valueType, out ulong value)
    {
      value = 0;
      valueType = null;

      var field = type.GetFieldByName(fieldName);
      if (field == null)
        return false;

      value = (ulong)field.GetValue(obj);
      if (value == 0)
        return false;

      valueType = _heap.GetObjectType(value);
      return valueType != null;
    }

    public override int MinCompletionPorts => _minCP;

    public override int MaxCompletionPorts => _maxCP;

    public override int CpuUtilization => _cpu;

    public override int FreeCompletionPortCount => _freeCP;

    public override int MaxFreeCompletionPorts => _maxFreeCP;
  }

  internal class DesktopManagedWorkItem : ManagedWorkItem
  {
    private readonly ClrType _type;
    private readonly ulong _addr;

    public DesktopManagedWorkItem(ClrType type, ulong addr)
    {
      _type = type;
      _addr = addr;
    }

    public override ulong Object => _addr;

    public override ClrType Type => _type;
  }

  internal class DesktopNativeWorkItem : NativeWorkItem
  {
    private readonly WorkItemKind _kind;
    private readonly ulong _callback;
    private readonly ulong _data;

    public DesktopNativeWorkItem(DacpWorkRequestData result)
    {
      _callback = result.Function;
      _data = result.Context;

      switch (result.FunctionType)
      {
        default:
        case WorkRequestFunctionTypes.UNKNOWNWORKITEM:
          _kind = WorkItemKind.Unknown;
          break;

        case WorkRequestFunctionTypes.TIMERDELETEWORKITEM:
          _kind = WorkItemKind.TimerDelete;
          break;

        case WorkRequestFunctionTypes.QUEUEUSERWORKITEM:
          _kind = WorkItemKind.QueueUserWorkItem;
          break;

        case WorkRequestFunctionTypes.ASYNCTIMERCALLBACKCOMPLETION:
          _kind = WorkItemKind.AsyncTimer;
          break;

        case WorkRequestFunctionTypes.ASYNCCALLBACKCOMPLETION:
          _kind = WorkItemKind.AsyncCallback;
          break;
      }
    }

    public DesktopNativeWorkItem(V45WorkRequestData result)
    {
      _callback = result.Function;
      _data = result.Context;
      _kind = WorkItemKind.Unknown;
    }

    public override WorkItemKind Kind => _kind;

    public override ulong Callback => _callback;

    public override ulong Data => _data;
  }
}