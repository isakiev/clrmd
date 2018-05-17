namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct LegacyJitCodeHeapInfo : ICodeHeap
  {
    public uint codeHeapType;
    public ulong address;
    public ulong currentAddr;

    public CodeHeapType Type => (CodeHeapType)codeHeapType;
    public ulong Address => address;
  }
}