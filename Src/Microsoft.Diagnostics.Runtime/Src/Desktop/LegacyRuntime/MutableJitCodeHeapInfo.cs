using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct MutableJitCodeHeapInfo : ICodeHeap
  {
    public CodeHeapType Type;
    public ulong Address;
    public ulong CurrentAddress;

    CodeHeapType ICodeHeap.Type => Type;
    ulong ICodeHeap.Address => Address;
  }
}