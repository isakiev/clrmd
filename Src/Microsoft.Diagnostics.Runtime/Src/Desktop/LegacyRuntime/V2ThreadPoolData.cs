#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V2ThreadPoolData : IThreadPoolData
  {
    private int _cpuUtilization;
    private int _numWorkerThreads;
    private int _minLimitTotalWorkerThreads;
    private int _maxLimitTotalWorkerThreads;
    private int _numRunningWorkerThreads;
    private int _numIdleWorkerThreads;
    private int _numQueuedWorkRequests;

    private ulong _firstWorkRequest;

    private uint _numTimers;

    private int _numCPThreads;
    private int _numFreeCPThreads;
    private int _maxFreeCPThreads;
    private int _numRetiredCPThreads;
    private int _maxLimitTotalCPThreads;
    private int _currentLimitTotalCPThreads;
    private int _minLimitTotalCPThreads;

    private ulong _QueueUserWorkItemCallbackFPtr;
    private ulong _AsyncCallbackCompletionFPtr;
    private ulong _AsyncTimerCallbackCompletionFPtr;

    public int MinCP => _minLimitTotalCPThreads;

    public int MaxCP => _maxLimitTotalCPThreads;

    public int CPU => _cpuUtilization;

    public int NumFreeCP => _numFreeCPThreads;

    public int MaxFreeCP => _maxFreeCPThreads;

    public int TotalThreads => _numWorkerThreads;

    public int RunningThreads => _numRunningWorkerThreads;

    public int IdleThreads => _numIdleWorkerThreads;

    public int MinThreads => _minLimitTotalWorkerThreads;

    public int MaxThreads => _maxLimitTotalWorkerThreads;

    ulong IThreadPoolData.FirstWorkRequest => _firstWorkRequest;

    public ulong QueueUserWorkItemCallbackFPtr => _QueueUserWorkItemCallbackFPtr;

    public ulong AsyncCallbackCompletionFPtr => _AsyncCallbackCompletionFPtr;

    public ulong AsyncTimerCallbackCompletionFPtr => _AsyncTimerCallbackCompletionFPtr;
  }
}