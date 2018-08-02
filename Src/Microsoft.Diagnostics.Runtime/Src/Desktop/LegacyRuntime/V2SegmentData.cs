﻿#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V2SegmentData : ISegmentData
  {
    public ulong segmentAddr;
    public ulong allocated;
    public ulong committed;
    public ulong reserved;
    public ulong used;
    public ulong mem;
    public ulong next;
    public ulong gc_heap;
    public ulong highAllocMark;

    public ulong Address => segmentAddr;
    public ulong Next => next;
    public ulong Start => mem;
    public ulong End => allocated;
    public ulong Reserved => reserved;
    public ulong Committed => committed;
  }
}