using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Utilities.Runtime
{
  public static class ClrRuntimeUtil
  {
    public static ICorDebugProcess TryGetCorDebugProcess(this ClrRuntime clrRuntime)
    {
      return clrRuntime is RuntimeBase runtime ? runtime.CorDebugProcess : null;
    }
  }
}