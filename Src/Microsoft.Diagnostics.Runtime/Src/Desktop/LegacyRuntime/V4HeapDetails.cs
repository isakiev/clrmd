#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V4HeapDetails : IHeapDetails
  {
    public ulong heapAddr; // Only filled in in server mode, otherwise NULL

    public ulong alloc_allocated;
    public ulong mark_array;
    public ulong c_allocate_lh;
    public ulong next_sweep_obj;
    public ulong saved_sweep_ephemeral_seg;
    public ulong saved_sweep_ephemeral_start;
    public ulong background_saved_lowest_address;
    public ulong background_saved_highest_address;

    public V4GenerationData generation_table0;
    public V4GenerationData generation_table1;
    public V4GenerationData generation_table2;
    public V4GenerationData generation_table3;
    public ulong ephemeral_heap_segment;
    public ulong finalization_fill_pointers0;
    public ulong finalization_fill_pointers1;
    public ulong finalization_fill_pointers2;
    public ulong finalization_fill_pointers3;
    public ulong finalization_fill_pointers4;
    public ulong finalization_fill_pointers5;
    public ulong finalization_fill_pointers6;
    public ulong lowest_address;
    public ulong highest_address;
    public ulong card_table;

    public ulong FirstHeapSegment => generation_table2.StartSegment;
    public ulong FirstLargeHeapSegment => generation_table3.StartSegment;
    public ulong EphemeralSegment => ephemeral_heap_segment;
    public ulong EphemeralEnd => alloc_allocated;
    public ulong EphemeralAllocContextPtr => generation_table0.AllocContextPtr;
    public ulong EphemeralAllocContextLimit => generation_table0.AllocContextLimit;

    public ulong Gen0Start => generation_table0.AllocationStart;
    public ulong Gen0Stop => alloc_allocated;
    public ulong Gen1Start => generation_table1.AllocationStart;
    public ulong Gen1Stop => generation_table0.AllocationStart;
    public ulong Gen2Start => generation_table2.AllocationStart;
    public ulong Gen2Stop => generation_table1.AllocationStart;

    public ulong FQAllObjectsStart => finalization_fill_pointers0;
    public ulong FQAllObjectsStop => finalization_fill_pointers3;
    public ulong FQRootsStart => finalization_fill_pointers3;
    public ulong FQRootsStop => finalization_fill_pointers5;
  }
}