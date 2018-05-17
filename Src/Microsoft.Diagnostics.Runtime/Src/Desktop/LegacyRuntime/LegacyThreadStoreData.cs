#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct LegacyThreadStoreData : IThreadStoreData
  {
    public int threadCount;
    public int unstartedThreadCount;
    public int backgroundThreadCount;
    public int pendingThreadCount;
    public int deadThreadCount;
    public ulong firstThread;
    public ulong finalizerThread;
    public ulong gcThread;
    public uint fHostConfig; // Uses hosting flags defined above

    public ulong Finalizer => finalizerThread;

    public int Count => threadCount;

    public ulong FirstThread => firstThread;
  }
}