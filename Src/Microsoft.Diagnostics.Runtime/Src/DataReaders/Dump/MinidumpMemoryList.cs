﻿namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  internal class MinidumpMemoryList
  {
    // Declaration of unmanaged structure is
    //   public ulong NumberOfMemoryRanges; // offset 0
    //   MINIDUMP_MEMORY_DESCRIPTOR[]; // var-length embedded array
    public MinidumpMemoryList(DumpPointer streamPointer)
    {
      _streamPointer = streamPointer;
    }

    private DumpPointer _streamPointer;

    public uint Count
    {
      get
      {
        long count = _streamPointer.ReadInt32();
        return (uint)count;
      }
    }

    public MINIDUMP_MEMORY_DESCRIPTOR GetElement(uint idx)
    {
      // Embededded array starts at offset 4.
      var offset = 4 + idx * MINIDUMP_MEMORY_DESCRIPTOR.SizeOf;
      return _streamPointer.PtrToStructure<MINIDUMP_MEMORY_DESCRIPTOR>(offset);
    }
  }
}