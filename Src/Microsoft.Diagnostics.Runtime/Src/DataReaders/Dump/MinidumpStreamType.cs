﻿namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  /// <summary>
  ///   Type of stream within the minidump.
  /// </summary>
  internal enum MINIDUMP_STREAM_TYPE
  {
    UnusedStream = 0,
    ReservedStream0 = 1,
    ReservedStream1 = 2,
    ThreadListStream = 3,
    ModuleListStream = 4,
    MemoryListStream = 5,
    ExceptionStream = 6,
    SystemInfoStream = 7,
    ThreadExListStream = 8,
    Memory64ListStream = 9,
    CommentStreamA = 10,
    CommentStreamW = 11,
    HandleDataStream = 12,
    FunctionTableStream = 13,
    UnloadedModuleListStream = 14,
    MiscInfoStream = 15,
    MemoryInfoListStream = 16,
    ThreadInfoListStream = 17,
    LastReservedStream = 0xffff
  }
}