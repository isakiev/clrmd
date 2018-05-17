namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopHeapInterface : ClrInterface
  {
    private readonly string _name;
    private readonly ClrInterface _base;

    public DesktopHeapInterface(string name, ClrInterface baseInterface)
    {
      _name = name;
      _base = baseInterface;
    }

    public override string Name => _name;

    public override ClrInterface BaseInterface => _base;
  }
}