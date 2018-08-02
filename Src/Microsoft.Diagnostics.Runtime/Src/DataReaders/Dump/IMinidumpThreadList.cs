namespace Microsoft.Diagnostics.Runtime.DataReaders.Dump
{
  internal interface IMinidumpThreadList
  {
    uint Count();
    MINIDUMP_THREAD GetElement(uint idx);
  }
}