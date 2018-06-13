namespace Microsoft.Diagnostics.Runtime
{
    internal enum DumpStreamType
    {
        Unused = 0,
        Reserved0 = 1,
        Reserved1 = 2,
        ThreadList = 3,
        ModuleList = 4,
        MemoryList = 5,
        Exception = 6,
        SystemInfo = 7,
        ThreadExList = 8,
        Memory64List = 9,
        CommentA = 10,
        CommentW = 11,
        HandleData = 12,
        FunctionTable = 13,
        UnloadedModuleList = 14,
        MiscInfo = 15,
        MemoryInfoList = 16,
        ThreadInfoList = 17,
        LastReserved = 0xffff
    }
}