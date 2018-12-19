using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;

// This provides a managed wrapper over the unmanaged dump-reading APIs in DbgHelp.dll.
// 
// ** This has several advantages:
// - type-safe wrappers
// - marshal minidump data-structures into the proper managed types (System.String,
// System.DateTime, System.Version, System.OperatingSystem, etc)
// 
// This does not validate aganst corrupted dumps. 
// 
// ** This is not a complete set of wrappers. 
// Other potentially interesting things to expose from the dump file:
// - the header. (Get flags, Version)
// - Exception stream (find last exception thrown)
// - 
// 
// ** Potential Performance improvements
// This was first prototyped in unmanaged C++, and was significantly faster there. 
// This is  not optimized for performance at all. It currently does not use unsafe C# and so has
// no pointers to structures and so has high costs from Marshal.PtrToStructure(typeof(T)) instead of
// just using T*. 
// This could probably be speed up signficantly (approaching the speed of the C++ prototype) by using unsafe C#. 
// 
// More data could be cached. A full dump may be 80 MB+, so caching extra data for faster indexing
// and lookup, especially for reading the memory stream.
// However, the app consuming the DumpReader is probably doing a lot of caching on its own, so
// extra caching in the dump reader may make microbenchmarks go faster, but just increase the
// working set and complexity of real clients.
// 
//     
// DumpReader:
//   Read contents of a minidump.
//   If we have a 32-bit dump, then there's an addressing collision possible.
//   OS debugging code sign extends 32 bit wide addresses into 64 bit wide addresses.
//   The CLR does not sign extend, thus you cannot round-trip target addresses exposed by this class.
//   Currently we read these addresses once and don't hand them back, so it's not an issue.

namespace Microsoft.Diagnostics.Runtime
{
    public class SimpleDataReader : IDataReader, IDisposable
    {
        private const uint DumpSignature = 0x504d444d;
        private const uint DumpVersion = 0xa793;
        private const uint DumpVersionMask = 0xffff;
        private const uint DumpWithFullMemoryInfo = 0x0002;

        private readonly BinaryReader myReader;
        private readonly DumpHeader myHeader;
        private readonly DumpSystemInfo mySystemInfo;
        private readonly IDictionary<DumpStreamType, DumpStreamDetails> myStreamDictionary;

        // Caching the chunks avoids the cost of Marshal.PtrToStructure on every single element in the memory list.
        // Empirically, this cache provides huge performance improvements for read memory.
        // This cache could be completey removed if we used unsafe C# and just had direct pointers
        // into the mapped dump file.
        private readonly DumpMemoryChunk[] myChunks;

        private IReadOnlyCollection<MINIDUMP_MODULE> myModules;
        private IReadOnlyCollection<MINIDUMP_THREAD> myThreads;

        public SimpleDataReader(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(filePath);

            myReader = new BinaryReader(File.OpenRead(filePath));
            myHeader = myReader.ReadStructure<DumpHeader>();

            if (myHeader.Singature != DumpSignature)
                throw new ClrDiagnosticsException("Incorrect dump signature");
            if ((myHeader.Version & DumpVersionMask) != DumpVersion)
                throw new ClrDiagnosticsException("Unsupported dump version");

            myStreamDictionary = new Dictionary<DumpStreamType, DumpStreamDetails>();
            myReader.Seek(myHeader.StreamDirectoryPosition);
            for (var i = 0; i < myHeader.NumberOfStreams; i++)
            {
                var details = myReader.ReadStructure<DumpStreamDetails>();
                if (details.Size > 0 && !myStreamDictionary.ContainsKey(details.Type))
                    myStreamDictionary.Add(details.Type, details);
            }

            var systemInfoStreamDetails = GetStreamDetails(DumpStreamType.SystemInfo);
            mySystemInfo = myReader.Seek(systemInfoStreamDetails.Position).ReadStructure<DumpSystemInfo>();
            IsMinidump = (myHeader.Flags & DumpWithFullMemoryInfo) == 0;

            List<DumpMemoryChunk> chunks;
            var isList64 = false;
            if (myStreamDictionary.TryGetValue(DumpStreamType.Memory64List, out var streamDetails64))
            {
                chunks = DumpMemoryChunkReader.ReadChunks64(myReader.Seek(streamDetails64.Position));
                isList64 = true;
            }
            else if (myStreamDictionary.TryGetValue(DumpStreamType.MemoryList, out var streamDetails))
                chunks = DumpMemoryChunkReader.ReadChunks32(myReader.Seek(streamDetails.Position));
            else
                throw new ClrDiagnosticsException("Dump doesn't contain MemoryList stream");

            myChunks = DumpMemoryChunkAggregator.MergeAndValidate(chunks, isList64).ToArray();
        }

        private DumpStreamDetails GetStreamDetails(DumpStreamType type)
        {
            if (!myStreamDictionary.TryGetValue(type, out var details))
                throw new ClrDiagnosticsException($"Dump doesn't contain {type} stream");

            return details;
        }

        public void Dispose()
        {
            myReader.Dispose();
        }

        public void Close()
        {
            Dispose();
        }

        public bool IsMinidump { get; }

        public void Flush()
        {
        }

        public Architecture GetArchitecture()
        {
            switch (mySystemInfo.ProcessorArchitecture)
            {
                case DumpProcessorArchitecture.INTEL:
                    return Architecture.X86;
                case DumpProcessorArchitecture.AMD64:
                    return Architecture.Amd64;
                case DumpProcessorArchitecture.ARM:
                    return Architecture.Arm;
                default:
                    return Architecture.Unknown;
            }
        }

        public uint GetPointerSize()
        {
            switch (GetArchitecture())
            {
                case Architecture.Amd64:
                    return 8;

                default:
                    return 4;
            }
        }

        private string ReadString(ContentPosition position)
        {
            myReader.Seek(position);

            // Minidump string is defined as:
            // typedef struct _MINIDUMP_STRING {
            //   ULONG32 Length;         // Length in bytes of the string
            //    WCHAR   Buffer [0];     // Variable size buffer
            // } MINIDUMP_STRING, *PMINIDUMP_STRING;
            var lengthBytes = myReader.ReadInt32();
            var bytes = myReader.ReadBytes(lengthBytes);

            return Encoding.Unicode.GetString(bytes);
        }

        private IReadOnlyCollection<MINIDUMP_MODULE> GetModules()
        {
            if (myModules != null)
                return myModules;

            var streamDetails = GetStreamDetails(DumpStreamType.ModuleList);
            myReader.Seek(streamDetails.Position);

            var entriesCount = myReader.ReadInt32();
            var result = new List<MINIDUMP_MODULE>(entriesCount);
            for (var i = 0; i < entriesCount; i++)
                result.Add(myReader.ReadStructure<MINIDUMP_MODULE>());

            myModules = result;
            return myModules;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            var result = new List<ModuleInfo>();

            foreach (var module in GetModules())
            {
                result.Add(
                    new ModuleInfo(this)
                    {
                        FileName = ReadString(new ContentPosition(module.ModuleNameRva.Value)),
                        ImageBase = module.BaseOfImage,
                        FileSize = module.SizeOfImage,
                        TimeStamp = module.TimeDateStamp,
                        Version = GetVersionInfo(module)
                    });
            }

            return result;
        }

        private IReadOnlyCollection<MINIDUMP_THREAD> GetThreads()
        {
            if (myThreads != null)
                return myThreads;

            var isThreadEx = false;

            // On x86 and X64, we have the ThreadListStream.  On IA64, we have the ThreadExListStream.
            if (!myStreamDictionary.TryGetValue(DumpStreamType.ThreadList, out var streamDetails))
            {
                streamDetails = GetStreamDetails(DumpStreamType.ThreadExList);
                isThreadEx = true;
            }

            myReader.Seek(streamDetails.Position);

            var entriesCount = myReader.ReadInt32();
            var result = new List<MINIDUMP_THREAD>(entriesCount);

            for (var i = 0; i < entriesCount; i++)
            {
                var entry = isThreadEx ? myReader.ReadStructure<MINIDUMP_THREAD_EX>() : myReader.ReadStructure<MINIDUMP_THREAD>();
                result.Add(entry);
            }

            myThreads = result;
            return result;
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            var module = TryLookupModuleByAddress(baseAddress);
            version = module != null ? GetVersionInfo(module) : new VersionInfo();
        }

        /// <summary>
        /// Return the module containing the target address, or null if no match.
        /// </summary>
        /// <param name="targetAddress">address in target</param>
        /// <returns>
        /// Null if no match. Else a DumpModule such that the target address is in between the range specified
        /// by the DumpModule's .BaseAddress and .Size property
        /// </returns>
        /// <remarks>
        /// This can be useful for symbol lookups or for using module images to
        /// supplement memory read requests for minidumps.
        /// </remarks>
        private MINIDUMP_MODULE TryLookupModuleByAddress(ulong targetAddress)
        {
            // This is an optimized lookup path, which avoids using IEnumerable or creating
            // unnecessary DumpModule objects.
            foreach (var module in GetModules())
            {
                var targetStart = module.BaseOfImage;
                var targetEnd = targetStart + module.SizeOfImage;
                if (targetStart <= targetAddress && targetEnd > targetAddress)
                    return module;
            }

            return null;
        }

        private static VersionInfo GetVersionInfo(MINIDUMP_MODULE module)
        {
            var version = module.VersionInfo;
            int minor = (ushort)version.dwFileVersionMS;
            int major = (ushort)(version.dwFileVersionMS >> 16);
            int patch = (ushort)version.dwFileVersionLS;
            int rev = (ushort)(version.dwFileVersionLS >> 16);

            var versionInfo = new VersionInfo(major, minor, rev, patch);
            return versionInfo;
        }

        private DumpMemoryChunk GetChunkContainingAddress(ulong address)
        {
            var targetChunk = new MinidumpMemoryChunk {TargetStartAddress = address};
            var index = Array.BinarySearch(myChunks, targetChunk);
            if (index >= 0)
            {
                Debug.Assert(myChunks[index].TargetStartAddress == address);
                return myChunks[index]; // exact match will contain the address
            }

            //TODO: revisit
            if (~index != 0)
            {
                var possibleIndex = Math.Min(myChunks.Length, ~index) - 1;
                if (myChunks[possibleIndex].TargetStartAddress <= address &&
                    myChunks[possibleIndex].TargetEndAddress > address)
                    return myChunks[possibleIndex];
            }

            return null;
        }

        public ulong ReadPointerUnsafe(ulong addr)
        {
            var chunk = GetChunkContainingAddress(addr);
            if (chunk == null)
                return 0;

            var offset = addr - chunk.TargetStartAddress;

            var position = new ContentPosition(chunk.ContentPosition.Value + (long)offset);
            myReader.Seek(position);

            //TODO: revisit assumption
            if (IntPtr.Size == 4)
                return myReader.ReadUInt32();

            return myReader.ReadUInt64();
        }

        public uint ReadDwordUnsafe(ulong addr)
        {
            throw new NotImplementedException();
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            bytesRead = ReadPartialMemory(address, buffer, bytesRequested);
            return bytesRead > 0;
        }

        public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            bytesRead = (int)ReadPartialMemory(address, buffer, (uint)bytesRequested);
            return bytesRead > 0;
        }

        public ulong GetThreadTeb(uint id)
        {
            var thread = GetThreads().FirstOrDefault(x => x.ThreadId == id);
            if (thread == null)
                return 0;

            return thread.Teb;
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            foreach (var thread in GetThreads())
                yield return thread.ThreadId;
        }

        public bool GetThreadContext(uint id, uint contextFlags, uint contextSize, IntPtr context)
        {
            var thread = GetThreads().FirstOrDefault(x => x.ThreadId == id);
            if (thread == null)
                return false;

            var buffer = context;
            var sizeBufferBytes = (int)contextSize;
            var loc = thread.ThreadContext;

            if (loc.IsNull) throw new ClrDiagnosticsException("Context not present", ClrDiagnosticsExceptionKind.CrashDumpError);

            var position = new ContentPosition(loc.Rva.Value);
            var sizeContext = loc.DataSize;

            if (sizeBufferBytes < sizeContext)
                throw new ClrDiagnosticsException(
                    "Context size mismatch. Expected = 0x" + sizeBufferBytes.ToString("x") + ", Size in dump = 0x" + sizeContext.ToString("x"),
                    ClrDiagnosticsExceptionKind.CrashDumpError);

            // Now copy from dump into buffer. 
            CopyContent(position, sizeContext, buffer);
            return true;
        }

        public bool GetThreadContext(uint threadId, uint contextFlags, uint contextSize, byte[] context)
        {
            throw new NotImplementedException();
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData data)
        {
            uint min = 0;
            uint max = (uint)myChunks.Length - 1;

            while (min <= max)
            {
                var mid = (max + min) / 2;

                var targetStartAddress = myChunks[mid].TargetStartAddress;

                if (addr < targetStartAddress)
                {
                    max = mid - 1;
                }
                else
                {
                    var targetEndAddress = myChunks[mid].TargetEndAddress;
                    if (targetEndAddress < addr)
                    {
                        min = mid + 1;
                    }
                    else
                    {
                        data = new VirtualQueryData(targetStartAddress, myChunks[mid].Size);
                        return true;
                    }
                }
            }

            data = new VirtualQueryData();
            return false;
        }

        /// <summary>
        /// Read memory from the dump file and return results in newly allocated buffer
        /// </summary>
        /// <param name="targetAddress">target address in dump to read length bytes from</param>
        /// <param name="length">number of bytes to read</param>
        /// <returns>newly allocated byte array containing dump memory</returns>
        /// <remarks>All memory requested must be readable or it throws.</remarks>
        private byte[] ReadMemory(ulong targetAddress, int length)
        {
            var buffer = new byte[length];
            ReadMemory(targetAddress, buffer, length);
            return buffer;
        }

        /// <summary>
        /// Read memory from the dump file and copy into the buffer
        /// </summary>
        /// <param name="targetAddress">target address in dump to read buffer.Length bytets from</param>
        /// <param name="buffer">destination buffer to copy target memory to.</param>
        /// <param name="cbRequestSize">count of bytes to read</param>
        /// <remarks>All memory requested must be readable or it throws.</remarks>
        private void ReadMemory(ulong targetAddress, byte[] buffer, int cbRequestSize)
        {
            var h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                ReadMemory(targetAddress, h.AddrOfPinnedObject(), (uint)cbRequestSize);
            }
            finally
            {
                h.Free();
            }
        }

        /// <summary>
        /// Read memory from target and copy it to the local buffer pointed to by
        /// destinationBuffer. Throw if any portion of the requested memory is unavailable.
        /// </summary>
        /// <param name="targetRequestStart">
        /// target address in dump file to copy
        /// destinationBufferSizeInBytes bytes from.
        /// </param>
        /// <param name="destinationBuffer">pointer to copy the memory to.</param>
        /// <param name="destinationBufferSizeInBytes">size of the destinationBuffer in bytes.</param>
        private void ReadMemory(ulong targetRequestStart, IntPtr destinationBuffer, uint destinationBufferSizeInBytes)
        {
            var bytesRead = ReadPartialMemory(targetRequestStart, destinationBuffer, destinationBufferSizeInBytes);
            if (bytesRead != destinationBufferSizeInBytes)
                throw new ClrDiagnosticsException(
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        "Memory missing at {0}. Could only read {1} bytes of {2} total bytes requested.",
                        targetRequestStart.ToString("x"),
                        bytesRead,
                        destinationBufferSizeInBytes),
                    ClrDiagnosticsExceptionKind.CrashDumpError);
        }

        /// <summary>
        /// Read memory from target and copy it to the local buffer pointed to by destinationBuffer.
        /// </summary>
        /// <param name="targetRequestStart">
        /// target address in dump file to copy
        /// destinationBufferSizeInBytes bytes from.
        /// </param>
        /// <param name="destinationBuffer">pointer to copy the memory to.</param>
        /// <param name="destinationBufferSizeInBytes">size of the destinationBuffer in bytes.</param>
        /// <returns>Number of contiguous bytes successfuly copied into the destination buffer.</returns>
        private uint ReadPartialMemory(ulong targetRequestStart, IntPtr destinationBuffer, uint destinationBufferSizeInBytes)
        {
            var bytesRead = ReadPartialMemoryInternal(
                targetRequestStart,
                destinationBuffer,
                destinationBufferSizeInBytes,
                0);
            return bytesRead;
        }

        private int ReadPartialMemory(ulong targetRequestStart, byte[] destinationBuffer, int bytesRequested)
        {
            if (bytesRequested <= 0)
                return 0;

            if (bytesRequested > destinationBuffer.Length)
                bytesRequested = destinationBuffer.Length;

            var bytesRead = 0;
            do
            {
                var chunk = GetChunkContainingAddress(targetRequestStart + (uint)bytesRead);
                if (chunk == null)
                    break;

                var pointerCurrentChunk = chunk.ContentPosition;
                var startAddr = targetRequestStart + (uint)bytesRead - chunk.TargetStartAddress;
                var bytesAvailable = chunk.Size - startAddr;

                Debug.Assert(bytesRequested >= bytesRead);
                var bytesToCopy = bytesRequested - bytesRead;
                if (bytesAvailable < (uint)bytesToCopy)
                    bytesToCopy = (int)bytesAvailable;

                Debug.Assert(bytesToCopy > 0);
                if (bytesToCopy == 0)
                    break;

                var adjustedPosition = new ContentPosition(pointerCurrentChunk.Value + (long)startAddr);
                myReader.Seek(adjustedPosition);

                myReader.BaseStream.Read(destinationBuffer, bytesRead, bytesToCopy);

                //TODO: check if copied less
                bytesRead += bytesToCopy;
            } while (bytesRead < bytesRequested);

            return bytesRead;
        }

        // Since a MemoryListStream makes no guarantees that there aren't duplicate, overlapping, or wholly contained
        // memory regions, we need to handle that.  For the purposes of this code, we presume all memory regions
        // in the dump that cover a given VA have the correct (duplicate) contents.
        private uint ReadPartialMemoryInternal(
            ulong targetRequestStart,
            IntPtr destinationBuffer,
            uint destinationBufferSizeInBytes,
            uint startIndex)
        {
            if (destinationBufferSizeInBytes == 0)
                return 0;

            uint bytesRead = 0;
            do
            {
                var chunk = GetChunkContainingAddress(targetRequestStart + bytesRead);
                if (chunk == null)
                    break;

                var pointerCurrentChunk = chunk.ContentPosition;
                var idxStart = (uint)(targetRequestStart + bytesRead - chunk.TargetStartAddress);
                var bytesAvailable = (uint)chunk.Size - idxStart;
                var bytesNeeded = destinationBufferSizeInBytes - bytesRead;
                var bytesToCopy = Math.Min(bytesAvailable, bytesNeeded);

                Debug.Assert(bytesToCopy > 0);
                if (bytesToCopy == 0)
                    break;

                var dest = new IntPtr(destinationBuffer.ToInt64() + bytesRead);
                var destSize = destinationBufferSizeInBytes - bytesRead;

                var adjustedPosition = new ContentPosition(pointerCurrentChunk.Value + idxStart);

                if (bytesToCopy > destSize)
                    throw new ArgumentException("Buffer too small");

                CopyContent(adjustedPosition, bytesToCopy, dest);

                bytesRead += bytesToCopy;
            } while (bytesRead < destinationBufferSizeInBytes);

            return bytesRead;
        }

        //TODO: revisit
        private void CopyContent(ContentPosition sourcePosition, uint bytesToCopy, IntPtr destination)
        {
            myReader.Seek(sourcePosition);
            var bytes = myReader.ReadBytes((int)bytesToCopy);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var source = handle.AddrOfPinnedObject();
                DumpReader.RtlMoveMemory(destination, source, new IntPtr(bytesToCopy));
            }
            finally
            {
                handle.Free();
            }
        }
    }
}