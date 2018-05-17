using System;

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct RWLockData : IRWLockData
  {
    public IntPtr pNext;
    public IntPtr pPrev;
    public int _uLockID;
    public int _lLockID;
    public short wReaderLevel;

    public ulong Next => (ulong)pNext.ToInt64();
    public int ULockID => _uLockID;
    public int LLockID => _lLockID;
    public int Level => wReaderLevel;
  }
}