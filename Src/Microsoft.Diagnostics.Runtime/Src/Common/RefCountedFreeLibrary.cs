using System;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime
{
  internal sealed class RefCountedFreeLibrary
  {
    private readonly IntPtr _library;
    private int _refCount;

    public RefCountedFreeLibrary(IntPtr library)
    {
      _library = library;
      _refCount = 1;
    }

    public void AddRef()
    {
      Interlocked.Increment(ref _refCount);
    }

    public int Release()
    {
      int count = Interlocked.Decrement(ref _refCount);
      if (count == 0 && _library != IntPtr.Zero)
        DataTarget.PlatformFunctions.FreeLibrary(_library);

      return count;
    }
  }
}