#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct DacpWorkRequestData
  {
    public WorkRequestFunctionTypes FunctionType;
    public ulong Function;
    public ulong Context;
    public ulong NextWorkRequest;
  }
}