using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
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

    public DesktopNativeWorkItem(WorkRequestData result)
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