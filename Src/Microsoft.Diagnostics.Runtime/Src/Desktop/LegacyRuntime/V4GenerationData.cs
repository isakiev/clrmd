#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V4GenerationData
  {
    public ulong StartSegment;
    public ulong AllocationStart;

    // These are examined only for generation 0, otherwise NULL
    public ulong AllocContextPtr;
    public ulong AllocContextLimit;
  }
}