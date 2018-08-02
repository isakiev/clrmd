#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct COMInterfacePointerData
  {
    public ulong MethodTable;
    public ulong InterfacePtr;
    public ulong ComContext;
  }
}