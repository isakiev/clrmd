namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopInterfaceData : ComInterfaceData
  {
    public override ClrType Type { get; }
    public override ulong InterfacePointer { get; }

    public DesktopInterfaceData(ClrType type, ulong ptr)
    {
      Type = type;
      InterfacePointer = ptr;
    }
  }
}