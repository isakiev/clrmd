namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopHeapInterface : ClrInterface
  {
    public DesktopHeapInterface(string name, ClrInterface baseInterface)
    {
      Name = name;
      BaseInterface = baseInterface;
    }

    public override string Name { get; }

    public override ClrInterface BaseInterface { get; }
  }
}