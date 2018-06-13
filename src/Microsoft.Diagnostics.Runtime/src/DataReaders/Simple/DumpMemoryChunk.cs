using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal class DumpMemoryChunk : IComparable<DumpMemoryChunk>
    {
        public readonly ulong Size;
        public readonly ulong TargetStartAddress;
        public readonly ulong TargetEndAddress; //TargetEndAddress is the first byte beyond the end of this chunk.
        public readonly ContentPosition ContentPosition;

        public DumpMemoryChunk(ulong size, ulong targetStartAddress, ContentPosition contentPosition)
        {
            Size = size;
            TargetStartAddress = targetStartAddress;
            TargetEndAddress = targetStartAddress + size;
            ContentPosition = contentPosition;
        }

        public int CompareTo(DumpMemoryChunk other)
        {
            return TargetStartAddress.CompareTo(other.TargetStartAddress);
        }
    }
}