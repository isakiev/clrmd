using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class HandleRoot : ClrRoot
  {
    private readonly GCRootKind _kind;
    private readonly string _name;
    private readonly ClrType _type;
    private readonly ClrAppDomain _domain;

    public HandleRoot(ulong addr, ulong obj, ClrType type, HandleType hndType, GCRootKind kind, ClrAppDomain domain)
    {
      _name = Enum.GetName(typeof(HandleType), hndType) + " handle";
      Address = addr;
      Object = obj;
      _kind = kind;
      _type = type;
      _domain = domain;
    }

    public override ClrAppDomain AppDomain => _domain;

    public override bool IsPinned => Kind == GCRootKind.Pinning || Kind == GCRootKind.AsyncPinning;

    public override GCRootKind Kind => _kind;

    public override string Name => _name;

    public override ClrType Type => _type;
  }
}