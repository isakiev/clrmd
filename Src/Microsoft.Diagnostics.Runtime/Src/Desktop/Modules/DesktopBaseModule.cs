using Microsoft.Diagnostics.Runtime.ComWrappers;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal abstract class DesktopBaseModule : ClrModule
  {
    protected DesktopRuntimeBase _runtime;

    public override ClrRuntime Runtime => _runtime;

    internal abstract ulong GetDomainModule(ClrAppDomain appDomain);

    internal ulong ModuleId { get; set; }

    internal virtual MetaDataImport GetMetadataImport()
    {
      return null;
    }

    public DesktopBaseModule(DesktopRuntimeBase runtime)
    {
      _runtime = runtime;
    }
  }
}