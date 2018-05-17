namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class FinalizerRoot : ClrRoot
  {
    private readonly ClrType _type;

    public FinalizerRoot(ulong obj, ClrType type)
    {
      Object = obj;
      _type = type;
    }

    public override GCRootKind Kind => GCRootKind.Finalizer;

    public override string Name => "finalization handle";

    public override ClrType Type => _type;
  }
}