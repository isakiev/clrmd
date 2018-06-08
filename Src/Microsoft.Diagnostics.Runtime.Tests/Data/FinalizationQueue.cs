using System;
using System.Threading;

public class FinalizationQueueTarget
{
  public const int ObjectsCount = 42;
  
  public static void Main(params string[] args)
  {
    Console.WriteLine(new DieHard());
    GC.Collect();
    
    for (var i = 0; i < ObjectsCount; i++)
      Console.WriteLine(new DieFast());
    
    GC.Collect();

    throw new Exception();
  }
}

public class DieHard
{
  private static readonly WaitHandle _handle = new ManualResetEvent(false);

  ~DieHard()
  {
    _handle.WaitOne();
  }
}

public class DieFast
{
  ~DieFast()
  {
    Console.WriteLine(GetHashCode());
  }
}
