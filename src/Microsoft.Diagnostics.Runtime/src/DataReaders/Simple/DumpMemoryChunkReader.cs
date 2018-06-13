using System.Collections.Generic;
using System.IO;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class DumpMemoryChunkReader
    {
        public static List<DumpMemoryChunk> ReadChunks32(BinaryReader reader)
        {
            var entriesCount = reader.ReadInt32();

            var result = new List<DumpMemoryChunk>();
            for (var i = 0; i < entriesCount; i++)
            {
                var targetStartAddress = reader.ReadUInt64();
                var size = reader.ReadUInt32();
                var contentPosition = new ContentPosition(reader.ReadUInt32());

                result.Add(new DumpMemoryChunk(size, targetStartAddress, contentPosition));
            }

            return result;
        }

        public static List<DumpMemoryChunk> ReadChunks64(BinaryReader reader)
        {
            var entriesCount = reader.ReadInt64();
            var basePosition = new ContentPosition(reader.ReadInt64());

            if (entriesCount > int.MaxValue)
                throw new ClrDiagnosticsException("Too many entries in memory range list");

            var currentPosition = basePosition;
            var result = new List<DumpMemoryChunk>();
            for (var i = 0; i < entriesCount; i++)
            {
                var targetStartAddress = reader.ReadUInt64();
                var size = reader.ReadUInt64();

                result.Add(new DumpMemoryChunk(size, targetStartAddress, currentPosition));
                currentPosition = new ContentPosition(currentPosition.Value + (long)size);
            }

            return result;
        }
    }
}