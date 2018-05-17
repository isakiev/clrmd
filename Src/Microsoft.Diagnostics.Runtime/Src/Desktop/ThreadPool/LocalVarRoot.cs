namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class LocalVarRoot : ClrRoot
  {
    private readonly bool _pinned;
    private readonly bool _falsePos;
    private readonly bool _interior;
    private readonly ClrThread _thread;
    private readonly ClrType _type;
    private readonly ClrAppDomain _domain;
    private readonly ClrStackFrame _stackFrame;

    public LocalVarRoot(ulong addr, ulong obj, ClrType type, ClrAppDomain domain, ClrThread thread, bool pinned, bool falsePos, bool interior, ClrStackFrame stackFrame)
    {
      Address = addr;
      Object = obj;
      _pinned = pinned;
      _falsePos = falsePos;
      _interior = interior;
      _domain = domain;
      _thread = thread;
      _type = type;
      _stackFrame = stackFrame;
    }

    public override ClrStackFrame StackFrame => _stackFrame;
    public override ClrAppDomain AppDomain => _domain;
    public override ClrThread Thread => _thread;
    public override bool IsPossibleFalsePositive => _falsePos;
    public override string Name => "local var";
    public override bool IsPinned => _pinned;
    public override GCRootKind Kind => GCRootKind.LocalVar;
    public override bool IsInterior => _interior;
    public override ClrType Type => _type;
  }
}