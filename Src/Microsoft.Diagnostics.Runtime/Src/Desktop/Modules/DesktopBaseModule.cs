using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal abstract class DesktopBaseModule : ClrModule
  {
    protected DesktopRuntimeBase _runtime;

    public override ClrRuntime Runtime => _runtime;

    internal abstract ulong GetDomainModule(ClrAppDomain appDomain);

    internal ulong ModuleId { get; set; }

    internal virtual IMetadataImport GetMetadataImport()
    {
      return null;
    }

    public int Revision { get; set; }

    public DesktopBaseModule(DesktopRuntimeBase runtime)
    {
      _runtime = runtime;
    }
  }
}