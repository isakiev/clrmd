namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class ThreadStaticVarRoot : ClrRoot
  {
    private readonly string _name;
    private readonly ClrAppDomain _domain;
    private readonly ClrType _type;

    public ThreadStaticVarRoot(ulong addr, ulong obj, ClrType type, string typeName, string variableName, ClrAppDomain appDomain)
    {
      Address = addr;
      Object = obj;
      _name = string.Format("thread static var {0}.{1}", typeName, variableName);
      _domain = appDomain;
      _type = type;
    }

    public override ClrAppDomain AppDomain => _domain;

    public override GCRootKind Kind => GCRootKind.ThreadStaticVar;

    public override string Name => _name;

    public override ClrType Type => _type;
  }
}