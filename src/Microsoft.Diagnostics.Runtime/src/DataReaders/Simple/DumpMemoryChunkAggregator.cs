using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class DumpMemoryChunkAggregator
    {
        public static IList<DumpMemoryChunk> MergeAndValidate(List<DumpMemoryChunk> chunks, bool isList64)
        {
            chunks.Sort();
            SplitAndMergeChunks(chunks);
            ValidateChunks(chunks, isList64);
            return chunks;
        }

        private static void SplitAndMergeChunks(IList<DumpMemoryChunk> chunks)
        {
            for (var i = 1; i < chunks.Count; i++)
            {
                var prevChunk = chunks[i - 1];
                var curChunk = chunks[i];

                // we already sorted
                Debug.Assert(prevChunk.TargetStartAddress <= curChunk.TargetStartAddress);

                // there is some overlap
                if (prevChunk.TargetEndAddress > curChunk.TargetStartAddress)
                {
                    // the previous chunk completely covers this chunk rendering it useless
                    if (prevChunk.TargetEndAddress >= curChunk.TargetEndAddress)
                    {
                        chunks.RemoveAt(i);
                        i--;
                    }
                    // previous chunk partially covers this one so we will remove the front
                    // of this chunk and resort it if needed
                    else
                    {
                        var overlap = prevChunk.TargetEndAddress - curChunk.TargetStartAddress;
                        curChunk = new DumpMemoryChunk(
                            curChunk.Size - overlap,
                            curChunk.TargetStartAddress + overlap,
                            new ContentPosition(curChunk.ContentPosition.Value + (long)overlap));
                        chunks[i] = curChunk;

                        // now that we changes the start address it might not be sorted anymore
                        // find the correct index
                        var newIndex = i;
                        for (; newIndex < chunks.Count - 1; newIndex++)
                            if (curChunk.TargetStartAddress <= chunks[newIndex + 1].TargetStartAddress)
                                break;

                        if (newIndex != i)
                        {
                            chunks.RemoveAt(i);
                            chunks.Insert(newIndex - 1, curChunk);
                            i--;
                        }
                    }
                }
            }
        }

        private static void ValidateChunks(IList<DumpMemoryChunk> chunks, bool isList64)
        {
            for (var i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].Size != chunks[i].TargetEndAddress - chunks[i].TargetStartAddress ||
                    chunks[i].TargetStartAddress > chunks[i].TargetEndAddress)
                    throw new ClrDiagnosticsException(
                        "Unexpected inconsistency error in dump memory chunk " + i
                        + " with target base address " + chunks[i].TargetStartAddress + ".",
                        ClrDiagnosticsExceptionKind.CrashDumpError);

                // If there's a next to compare to, and it's a MinidumpWithFullMemory, then we expect
                // that the RVAs & addresses will all be sorted in the dump.
                // MinidumpWithFullMemory stores things in a Memory64ListStream.
                if (i < chunks.Count - 1 && isList64 &&
                    (chunks[i].ContentPosition.Value >= chunks[i + 1].ContentPosition.Value ||
                    chunks[i].TargetEndAddress > chunks[i + 1].TargetStartAddress))
                    throw new ClrDiagnosticsException(
                        "Unexpected relative addresses inconsistency between dump memory chunks "
                        + i + " and " + (i + 1) + ".",
                        ClrDiagnosticsExceptionKind.CrashDumpError);

                // Because we sorted and split/merged entries we can expect them to be increasing and non-overlapping
                if (i < chunks.Count - 1 && chunks[i].TargetEndAddress > chunks[i + 1].TargetStartAddress)
                    throw new ClrDiagnosticsException("Unexpected overlap between memory chunks", ClrDiagnosticsExceptionKind.CrashDumpError);
            }
        }
    }
}