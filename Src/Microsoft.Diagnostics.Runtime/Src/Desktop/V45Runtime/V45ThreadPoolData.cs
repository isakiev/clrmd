#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct V45ThreadPoolData : IThreadPoolData
  {
    private int _cpuUtilization;
    private int _numIdleWorkerThreads;
    private int _numWorkingWorkerThreads;
    private int _numRetiredWorkerThreads;
    private int _minLimitTotalWorkerThreads;
    private int _maxLimitTotalWorkerThreads;

    private ulong _firstUnmanagedWorkRequest;

    private ulong _hillClimbingLog;
    private int _hillClimbingLogFirstIndex;
    private int _hillClimbingLogSize;

    private int _numTimers;

    private int _numCPThreads;
    private int _numFreeCPThreads;
    private int _maxFreeCPThreads;
    private int _numRetiredCPThreads;
    private int _maxLimitTotalCPThreads;
    private int _currentLimitTotalCPThreads;
    private int _minLimitTotalCPThreads;

    private ulong _asyncTimerCallbackCompletionFPtr;

    public int MinCP => _minLimitTotalCPThreads;

    public int MaxCP => _maxLimitTotalCPThreads;

    public int CPU => _cpuUtilization;

    public int NumFreeCP => _numFreeCPThreads;

    public int MaxFreeCP => _maxFreeCPThreads;

    public int TotalThreads => _numIdleWorkerThreads + _numWorkingWorkerThreads + _numRetiredWorkerThreads;

    public int RunningThreads => _numWorkingWorkerThreads;

    public int IdleThreads => _numIdleWorkerThreads;

    public int MinThreads => _minLimitTotalWorkerThreads;

    public int MaxThreads => _maxLimitTotalWorkerThreads;

    public ulong FirstWorkRequest => _firstUnmanagedWorkRequest;

    public ulong QueueUserWorkItemCallbackFPtr => ulong.MaxValue;

    public ulong AsyncCallbackCompletionFPtr => ulong.MaxValue;

    ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr => ulong.MaxValue;
  }
}