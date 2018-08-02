namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopManagedWorkItem : ManagedWorkItem
  {
    private readonly ClrType _type;
    private readonly ulong _addr;

    public DesktopManagedWorkItem(ClrType type, ulong addr)
    {
      _type = type;
      _addr = addr;
    }

    public override ulong Object => _addr;
    public override ClrType Type => _type;
  }
}