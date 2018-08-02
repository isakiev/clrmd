using System;

namespace Microsoft.Diagnostics.Runtime.Tests.Data
{
  public class DumpDebugTarget
  {
    public static void Main()
    {
      Method(0);
    }

    public static void Method(int n)
    {
      if (n == 10)
      {
        throw new Exception("Count = " + n);
      }
      Method(n + 1);
    }
  }
}